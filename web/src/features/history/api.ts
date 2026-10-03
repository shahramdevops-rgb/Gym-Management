import { keepPreviousData, useQuery } from "@tanstack/react-query";

import type { PaymentMethod } from "@/features/payments/api";
import type { ServiceChargeKind } from "@/features/serviceCharges/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type HistoryAttendance = components["schemas"]["HistoryAttendanceResponse"];
export type HistoryPayment = components["schemas"]["HistoryPaymentResponse"];
export type HistoryServiceCharge = components["schemas"]["HistoryServiceChargeResponse"];
/** What a payment was for: a subscription, هوازی or the cafe. */
export type PaymentSource = components["schemas"]["PaymentTargetKind"];

export const historyPageSize = 20;

/** The order the "بابت" filter lists them in, and their names on screen. */
export const paymentSources: PaymentSource[] = ["Subscription", "ServiceCharge", "CafeOrder"];

export const paymentSourceLabels: Record<PaymentSource, string> = {
  Subscription: "اشتراک",
  ServiceCharge: "هوازی",
  CafeOrder: "بوفه",
};

/**
 * What the "بابت" filter offers. هوازی, فروشگاه and آنالیز are all service charges to the API, but
 * separate sources on screen (BUSINESS_RULES.md §7 *Sale at the desk*), so the filter names them
 * apart and `paymentSourceQuery` turns the choice back into the API's two parameters.
 */
export type PaymentSourceFilter =
  | "Subscription"
  | "Cardio"
  | "Miscellaneous"
  | "Analysis"
  | "CafeOrder";

export const paymentSourceFilters: PaymentSourceFilter[] = [
  "Subscription",
  "Cardio",
  "Miscellaneous",
  "Analysis",
  "CafeOrder",
];

export const paymentSourceFilterLabels: Record<PaymentSourceFilter, string> = {
  Subscription: "اشتراک",
  Cardio: "هوازی",
  Miscellaneous: "فروشگاه",
  Analysis: "آنالیز",
  CafeOrder: "بوفه",
};

export function paymentSourceQuery(source: PaymentSourceFilter | undefined): {
  Source?: PaymentSource;
  ServiceKind?: ServiceChargeKind;
} {
  switch (source) {
    case undefined:
      return {};
    case "Cardio":
    case "Miscellaneous":
    case "Analysis":
      return { Source: "ServiceCharge", ServiceKind: source };
    default:
      return { Source: source };
  }
}

export type HistorySale = components["schemas"]["HistorySaleResponse"];
/** What kind of sale a row is: a plan, هوازی, فروشگاه, آنالیز or the cafe. */
export type SaleSource = components["schemas"]["SaleSource"];
/** «پرداخت شده» or «پرداخت نشده»; a partly paid sale is unpaid (BUSINESS_RULES.md §12 Sales). */
export type SalePaidFilter = "Paid" | "Unpaid";

export const salePaidFilters: SalePaidFilter[] = ["Paid", "Unpaid"];

export const salePaidFilterLabels: Record<SalePaidFilter, string> = {
  Paid: "پرداخت شده",
  Unpaid: "پرداخت نشده",
};

export const saleSourceLabels: Record<SaleSource, string> = {
  Subscription: "پلن",
  Cardio: "هوازی",
  Miscellaneous: "فروشگاه",
  Analysis: "آنالیز",
  CafeOrder: "بوفه",
};

/** What every section filters by. A date left out is no bound on that side. */
export interface HistoryFilter {
  from?: string;
  to?: string;
  memberId?: string;
  page: number;
}

export interface PaymentHistoryFilter extends HistoryFilter {
  method?: PaymentMethod;
  source?: PaymentSourceFilter;
}

export interface SalesHistoryFilter extends HistoryFilter {
  /** Left out for «همهٔ فروش‌ها». */
  source?: SaleSource;
  paid?: SalePaidFilter;
}

/**
 * Query keys. Its own root, not under "payments" or "attendance": nothing on this page changes
 * anything, and a list here is fetched again whenever the page opens (the default `staleTime` of 0).
 */
export const historyKeys = {
  all: ["history"] as const,
  attendance: (filter: HistoryFilter) => [...historyKeys.all, "attendance", filter] as const,
  payments: (filter: PaymentHistoryFilter) => [...historyKeys.all, "payments", filter] as const,
  serviceCharges: (filter: HistoryFilter) =>
    [...historyKeys.all, "service-charges", filter] as const,
  sales: (filter: SalesHistoryFilter) => [...historyKeys.all, "sales", filter] as const,
};

function paged<T>(data: { items: T[]; totalCount: number | string }) {
  return {
    items: data.items,
    totalCount: Number(data.totalCount),
    pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / historyPageSize)),
  };
}

/** Every check-in in the gym, newest first (BUSINESS_RULES.md §12 History). */
export function useAttendanceHistory(filter: HistoryFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: historyKeys.attendance(filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/attendance", {
        params: {
          query: {
            From: filter.from,
            To: filter.to,
            MemberId: filter.memberId,
            Page: filter.page,
            PageSize: historyPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return paged(data);
    },
  });
}

/**
 * Every payment and refund, newest first. Staff are refused anything before today and the 3 days
 * before it (`Payments.HistoryTooFarBack`); the page checks that first so it never asks.
 */
export function usePaymentHistory(filter: PaymentHistoryFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: historyKeys.payments(filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/payments", {
        params: {
          query: {
            From: filter.from,
            To: filter.to,
            MemberId: filter.memberId,
            Method: filter.method,
            ...paymentSourceQuery(filter.source),
            Page: filter.page,
            PageSize: historyPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return paged(data);
    },
  });
}

/**
 * Everything the gym sold, newest first, with what has been paid on it (BUSINESS_RULES.md §12
 * Sales in the history). The Owner's alone: the API refuses Staff, so the page never asks for them.
 */
export function useSalesHistory(filter: SalesHistoryFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: historyKeys.sales(filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/sales", {
        params: {
          query: {
            From: filter.from,
            To: filter.to,
            MemberId: filter.memberId,
            Source: filter.source,
            Paid: filter.paid,
            Page: filter.page,
            PageSize: historyPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return paged(data);
    },
  });
}

/** Every هوازی charge and sale (فروشگاه, آنالیز), voided ones included, newest first. */
export function useServiceChargeHistory(filter: HistoryFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: historyKeys.serviceCharges(filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/service-charges", {
        params: {
          query: {
            From: filter.from,
            To: filter.to,
            MemberId: filter.memberId,
            Page: filter.page,
            PageSize: historyPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return paged(data);
    },
  });
}
