import { keepPreviousData, useQuery } from "@tanstack/react-query";

import type { components } from "@/lib/api/schema";

import { siteApi } from "../api/client";

export type MyDetails = components["schemas"]["MyDetailsResponse"];
export type MyPlan = components["schemas"]["MyPlanResponse"];
export type SitePlan = components["schemas"]["SitePlanResponse"];
export type MyDebt = components["schemas"]["MyDebtResponse"];
export type MyDebtItem = components["schemas"]["MyDebtItemResponse"];
export type MyVisit = components["schemas"]["MyVisitResponse"];
export type MyPayment = components["schemas"]["MyPaymentResponse"];
export type MyCafeOrder = components["schemas"]["MyCafeOrderResponse"];

/** Every key starts here, so a sign-out's `clear()` and a test can reach them all. */
export const myRecordsQueryKey = ["site", "me", "records"] as const;

/** Rows a history tab shows at once: a phone screen or two. */
export const historyPageSize = 10;

/*
 * The member's own records (BUSINESS_RULES.md §15 *The member's own records*, roadmap 15.5),
 * read-only: no path names a member, each answers with the signed-in member's own. The cards
 * that change when the desk does something (the plan, the debt) are read again when the app
 * comes back to the front, as «مربی من» is.
 */

export function useMyDetails() {
  return useQuery({
    queryKey: [...myRecordsQueryKey, "details"],
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/me/details");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** The plan now and the one bought for later, either, both or neither. */
export function useMyPlan() {
  return useQuery({
    queryKey: [...myRecordsQueryKey, "plan"],
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/me/plan");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

export function useMyDebt() {
  return useQuery({
    queryKey: [...myRecordsQueryKey, "debt"],
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/me/debt");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** One page of a history, with its page count worked out as the panel's lists do. */
function toPage<T>(data: { items: T[]; totalCount: number | string }) {
  return {
    items: data.items,
    pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / historyPageSize)),
  };
}

/**
 * A history tab's page, read only while its tab is open; the page shown stays on screen while
 * the next one loads, so the pager does not jump.
 */
function useHistory<T>(
  name: string,
  page: number,
  enabled: boolean,
  fetchPage: (query: { Page: number; PageSize: number }) => Promise<{
    data?: { items: T[]; totalCount: number | string };
    error?: unknown;
  }>,
) {
  return useQuery({
    queryKey: [...myRecordsQueryKey, name, page],
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await fetchPage({ Page: page, PageSize: historyPageSize });
      if (error !== undefined || data === undefined) {
        throw error;
      }
      return toPage(data);
    },
  });
}

/** Check-ins and check-outs with their lockers, newest first. */
export function useMyVisits(page: number, enabled: boolean) {
  return useHistory<MyVisit>("visits", page, enabled, (query) =>
    siteApi.GET("/api/site/me/visits", { params: { query } }),
  );
}

export function useMyPlans(page: number, enabled: boolean) {
  return useHistory<SitePlan>("plans", page, enabled, (query) =>
    siteApi.GET("/api/site/me/plans", { params: { query } }),
  );
}

export function useMyPayments(page: number, enabled: boolean) {
  return useHistory<MyPayment>("payments", page, enabled, (query) =>
    siteApi.GET("/api/site/me/payments", { params: { query } }),
  );
}

export function useMyCafeOrders(page: number, enabled: boolean) {
  return useHistory<MyCafeOrder>("cafe-orders", page, enabled, (query) =>
    siteApi.GET("/api/site/me/cafe-orders", { params: { query } }),
  );
}
