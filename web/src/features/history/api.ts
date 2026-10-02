import { keepPreviousData, useQuery } from "@tanstack/react-query";

import type { PaymentMethod } from "@/features/payments/api";
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

/** What every section filters by. A date left out is no bound on that side. */
export interface HistoryFilter {
  from?: string;
  to?: string;
  memberId?: string;
  page: number;
}

export interface PaymentHistoryFilter extends HistoryFilter {
  method?: PaymentMethod;
  source?: PaymentSource;
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
            Source: filter.source,
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

/** Every هوازی charge, voided ones included, newest first. */
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
