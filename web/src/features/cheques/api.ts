import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { reportKeys } from "@/features/dashboard/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Cheque = components["schemas"]["ChequeResponse"];
export type ChequeStatus = components["schemas"]["ChequeStatus"];

export const chequesPageSize = 20;

/** BUSINESS_RULES.md §9 *Cheques*, mirroring Cheque. */
export const chequeLimits = {
  payeeMaxLength: 200,
  descriptionMaxLength: 500,
  cancelReasonMaxLength: 500,
} as const;

/** The three statuses in the register's order, and their names on screen. */
export const chequeStatuses: readonly ChequeStatus[] = ["Pending", "Passed", "Cancelled"];

export const chequeStatusLabels: Record<ChequeStatus, string> = {
  Pending: "در انتظار",
  Passed: "پاس شد",
  Cancelled: "باطل شده",
};

/**
 * Query keys. Every key starts with "cheques", so one invalidation after any change refreshes
 * every tab and the pending total together.
 */
export const chequeKeys = {
  all: ["cheques"] as const,
  list: (filter: ChequeListFilter) => [...chequeKeys.all, "list", filter] as const,
};

export interface ChequeListFilter {
  /** Omitted: every cheque. */
  status?: ChequeStatus;
  page: number;
}

/**
 * One page of the register: pending cheques the earliest date first, any other list the latest
 * first, with the total of every pending cheque whatever the tab (BUSINESS_RULES.md §9 *Cheques*).
 */
export function useChequeList(filter: ChequeListFilter) {
  return useQuery({
    queryKey: chequeKeys.list(filter),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/cheques", {
        params: {
          query: { Status: filter.status, Page: filter.page, PageSize: chequesPageSize },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pendingTotal: data.pendingTotal,
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / chequesPageSize)),
      };
    },
  });
}

/**
 * Every change also refreshes the dashboard's needs-attention lists, where the cheques coming due
 * are shown, so going back to the dashboard never shows a cheque already passed.
 */
function useChequeMutation<TArgs, TResult>(request: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: chequeKeys.all }),
        queryClient.invalidateQueries({ queryKey: reportKeys.needsAttention() }),
      ]);
    },
  });
}

export interface ChequeInput {
  /** The plain decimal string the API reads (`normalizeMoney`), never a float. */
  amount: string;
  /** ISO business date: the date written on the cheque. */
  dueDate: string;
  payee: string;
  description: string;
}

export function useRegisterCheque() {
  return useChequeMutation(async (body: ChequeInput) => {
    const { data, error } = await api.POST("/api/cheques", { body });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useUpdateCheque() {
  return useChequeMutation(
    async ({ id, ...body }: ChequeInput & { id: string; version: number | string }) => {
      const { data, error } = await api.PUT("/api/cheques/{id}", {
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

export function useMarkChequePassed() {
  return useChequeMutation(async (id: string) => {
    const { data, error } = await api.POST("/api/cheques/{id}/pass", {
      params: { path: { id } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useCancelCheque() {
  return useChequeMutation(async ({ id, reason }: { id: string; reason: string }) => {
    const { data, error } = await api.POST("/api/cheques/{id}/cancel", {
      params: { path: { id } },
      body: { reason },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
