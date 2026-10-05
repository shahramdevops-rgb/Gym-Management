import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { reportKeys } from "@/features/dashboard/api";
import { expenseKeys } from "@/features/expenses/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { toPersianDigits } from "@/lib/format";

export type Payable = components["schemas"]["PayableResponse"];
export type PayableKind = components["schemas"]["PayableKind"];
export type PayableStatus = components["schemas"]["PayableStatus"];
export type PayableDueSoon = components["schemas"]["PayableDueSoonResponse"];

export const payablesPageSize = 20;

/** BUSINESS_RULES.md §9 *Cheques and instalments*, mirroring Payable. */
export const payableLimits = {
  payeeMaxLength: 200,
  descriptionMaxLength: 500,
  reasonMaxLength: 500,
  maxInstallmentCount: 360,
} as const;

/** The three statuses in the register's order. */
export const payableStatuses: readonly PayableStatus[] = ["Pending", "Paid", "Cancelled"];

export const payableKinds: readonly PayableKind[] = ["Cheque", "Installment"];

export const payableKindLabels: Record<PayableKind, string> = {
  Cheque: "چک",
  Installment: "قسط",
};

/** The status tabs: «پرداخت شد» covers a passed cheque and a paid instalment alike. */
export const payableStatusLabels: Record<PayableStatus, string> = {
  Pending: "در انتظار",
  Paid: "پرداخت شد",
  Cancelled: "باطل شده",
};

/** A paid row says it the way the Owner does: a cheque is «پاس شد», an instalment «پرداخت شد». */
export function paidLabel(kind: PayableKind): string {
  return kind === "Cheque" ? "پاس شد" : "پرداخت شد";
}

/** «قسط ۳ از ۱۲» for an instalment, «چک» for a cheque. */
export function kindText(payable: {
  kind: PayableKind;
  installmentNumber?: number | string | null;
  installmentCount?: number | string | null;
}): string {
  if (payable.kind === "Cheque" || payable.installmentNumber == null) {
    return payableKindLabels[payable.kind];
  }
  const number = toPersianDigits(payable.installmentNumber);
  const count = toPersianDigits(payable.installmentCount ?? "");
  return `قسط ${number} از ${count}`;
}

/**
 * Query keys. Every key starts with "payables", so one invalidation after any change refreshes
 * every tab and the pending totals together.
 */
export const payableKeys = {
  all: ["payables"] as const,
  list: (filter: PayableListFilter) => [...payableKeys.all, "list", filter] as const,
  dueSoon: () => [...payableKeys.all, "due-soon"] as const,
};

/**
 * The header's alert starts this many days before a date, today included (BUSINESS_RULES.md §9
 * *Cheques and instalments*). The API owns it (`ListPayablesDueSoonHandler.WithinDays`); this copy
 * only lets the alert say which rule it follows.
 */
export const payableAlertWithinDays = 5;

/** How often the header asks again, so a payment comes into the alert on the day it should. */
const dueSoonRefreshMs = 5 * 60 * 1000;

/** «امروز», «فردا», «۳ روز دیگر», «۲ روز گذشته»: how far a payment is from today. */
export function daysLeftText(daysLeft: number): string {
  if (daysLeft < 0) {
    return `${toPersianDigits(-daysLeft)} روز گذشته`;
  }
  if (daysLeft === 0) {
    return "امروز";
  }
  if (daysLeft === 1) {
    return "فردا";
  }
  return `${toPersianDigits(daysLeft)} روز دیگر`;
}

/**
 * Pending cheques and instalments within 5 days or past their date, the earliest first, for the
 * header on every page. Owner only: the header renders the alert for the Owner alone.
 * It is one small query, so unlike the rest of the app it also asks again when the window comes
 * back into focus.
 */
export function usePayablesDueSoon() {
  return useQuery({
    queryKey: payableKeys.dueSoon(),
    refetchInterval: dueSoonRefreshMs,
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/payables/due-soon");
      if (error !== undefined) {
        throw error;
      }
      return {
        today: data.today,
        items: data.items.map((item) => ({ ...item, daysLeft: Number(item.daysLeft) })),
      };
    },
  });
}

export interface PayableListFilter {
  /** Omitted: every status. */
  status?: PayableStatus;
  /** Omitted: cheques and instalments both. */
  kind?: PayableKind;
  page: number;
}

/**
 * One page of the register: pending ones the earliest date first, any other list the latest first,
 * with what is still pending — in all, and split into cheques and instalments — whatever the
 * filter (BUSINESS_RULES.md §9 *Cheques and instalments*).
 */
export function usePayableList(filter: PayableListFilter) {
  return useQuery({
    queryKey: payableKeys.list(filter),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/payables", {
        params: {
          query: {
            Status: filter.status,
            Kind: filter.kind,
            Page: filter.page,
            PageSize: payablesPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pendingTotal: data.pendingTotal,
        pendingChequeTotal: data.pendingChequeTotal,
        pendingInstallmentTotal: data.pendingInstallmentTotal,
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / payablesPageSize)),
      };
    },
  });
}

/**
 * Every change also refreshes the dashboard's needs-attention lists, where the ones coming due are
 * shown, and the expenses, which paying or reverting writes to.
 */
function usePayableMutation<TArgs, TResult>(request: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: payableKeys.all }),
        queryClient.invalidateQueries({ queryKey: reportKeys.needsAttention() }),
        queryClient.invalidateQueries({ queryKey: expenseKeys.all }),
      ]);
    },
  });
}

export interface PayableInput {
  kind: PayableKind;
  /** The plain decimal string the API reads (`normalizeMoney`), never a float. */
  amount: string;
  /** ISO business date: the date on the cheque, or the instalment's due day. */
  dueDate: string;
  payee: string;
  description: string;
  categoryId: string;
  /** «قسط n از N»: both for an instalment, both null for a cheque. */
  installmentNumber: number | null;
  installmentCount: number | null;
}

export function useRegisterPayable() {
  return usePayableMutation(async (body: PayableInput) => {
    const { data, error } = await api.POST("/api/payables", { body });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useUpdatePayable() {
  return usePayableMutation(
    async ({ id, ...body }: PayableInput & { id: string; version: number | string }) => {
      const { data, error } = await api.PUT("/api/payables/{id}", {
        params: { path: { id } },
        body,
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}

export function useMarkPayablePaid() {
  return usePayableMutation(async (id: string) => {
    const { data, error } = await api.POST("/api/payables/{id}/pay", {
      params: { path: { id } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useRevertPayable() {
  return usePayableMutation(async ({ id, reason }: { id: string; reason: string }) => {
    const { data, error } = await api.POST("/api/payables/{id}/revert", {
      params: { path: { id } },
      body: { reason },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useCancelPayable() {
  return usePayableMutation(async ({ id, reason }: { id: string; reason: string }) => {
    const { data, error } = await api.POST("/api/payables/{id}/cancel", {
      params: { path: { id } },
      body: { reason },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
