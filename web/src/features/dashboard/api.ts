import { keepPreviousData, useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

import type { ReportRange } from "./range";

export type FinancialReport = components["schemas"]["FinancialReportResponse"];
export type FinancialPeriod = components["schemas"]["FinancialPeriodResponse"];
export type RevenueSource = components["schemas"]["RevenueSource"];
export type AttendanceReport = components["schemas"]["AttendanceReportResponse"];
export type MembersReport = components["schemas"]["MembersReportResponse"];
export type TopCafeProduct = components["schemas"]["TopCafeProductResponse"];
export type Receivables = components["schemas"]["ReceivablesResponse"];
export type SubscriptionsSnapshot = components["schemas"]["SubscriptionsSnapshotResponse"];
export type NeedsAttention = components["schemas"]["NeedsAttentionResponse"];

/**
 * The numbers the reports judge by. The API owns them (`ReportThresholds`); this copy only lets
 * the dashboard say which rule put someone on a list.
 */
export const reportThresholds = {
  lowSessions: 3,
  expiringWithinDays: 5,
  absentDays: 10,
  /** Both "left in the last 30 days" and "an old debt" (§12 *Needs attention*). */
  windowDays: 30,
  /** A pending cheque or instalment is shown this many days before its date (§9 *Cheques and instalments*). */
  payableDueWithinDays: 7,
} as const;

/**
 * The order the dashboard lists the sources in: the API's, with the cafe moved above فروشگاه
 * (asked by the developer, 1405/07/13). The API keeps its own order for every other reader.
 */
export const revenueSourceOrder: RevenueSource[] = [
  "Membership",
  "SingleSession",
  "Cardio",
  "Cafe",
  "Miscellaneous",
  "Analysis",
  "Other",
];

/**
 * The sources whose bar says how many were sold in the range (asked by the developer,
 * 1405/07/13): plans sold matter more to the Owner than new members. What one of them is called.
 */
export const soldCountNouns: Partial<Record<RevenueSource, string>> = {
  Membership: "پلن",
  SingleSession: "تک‌جلسه",
};

/** The seven sources in the API's own order, and their names on screen (§12 *Financial report*). */
export const revenueSourceLabels: Record<RevenueSource, string> = {
  Membership: "پلن",
  SingleSession: "تک‌جلسه‌ای",
  Cardio: "هوازی",
  Miscellaneous: "فروشگاه",
  Analysis: "آنالیز",
  Other: "متفرقه",
  Cafe: "بوفه",
};

/**
 * Query keys. Reports change whenever money or a visit is recorded anywhere, so nothing here is
 * invalidated by hand: each one is fetched again when the dashboard opens (the default
 * `staleTime` of 0).
 */
export const reportKeys = {
  all: ["reports"] as const,
  financial: (range: ReportRange) => [...reportKeys.all, "financial", range] as const,
  attendance: (range: ReportRange) => [...reportKeys.all, "attendance", range] as const,
  members: (range: ReportRange) => [...reportKeys.all, "members", range] as const,
  cafeProducts: (range: ReportRange) => [...reportKeys.all, "cafe-products", range] as const,
  receivables: () => [...reportKeys.all, "receivables"] as const,
  subscriptions: () => [...reportKeys.all, "subscriptions"] as const,
  needsAttention: () => [...reportKeys.all, "needs-attention"] as const,
};

function rangeQuery(range: ReportRange) {
  return { params: { query: { From: range.from, To: range.to } } };
}

interface RangeOptions {
  /** False while the range is one the API would refuse. */
  enabled: boolean;
}

/** Money of the range and of the range before it, and day by day (§12 *Financial report*). */
export function useFinancialReport(range: ReportRange, { enabled }: RangeOptions) {
  return useQuery({
    queryKey: reportKeys.financial(range),
    enabled,
    // A new range keeps the old figures on screen until its own arrive, instead of a blank page.
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/reports/financial", rangeQuery(range));
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Members' visits of the range, by day and by weekday × hour (§12 *Operational reports*). */
export function useAttendanceReport(range: ReportRange, { enabled }: RangeOptions) {
  return useQuery({
    queryKey: reportKeys.attendance(range),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/reports/attendance", rangeQuery(range));
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Renewals and new members of the range, day by day (§12 *Operational reports*). */
export function useMembersReport(range: ReportRange, { enabled }: RangeOptions) {
  return useQuery({
    queryKey: reportKeys.members(range),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/reports/members", rangeQuery(range));
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** The cafe's 10 best sellers of the range by quantity (§12 *Operational reports*). */
export function useTopCafeProducts(range: ReportRange, { enabled }: RangeOptions) {
  return useQuery({
    queryKey: reportKeys.cafeProducts(range),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/reports/cafe-products", rangeQuery(range));
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** What everyone owes now, by age; no range (§12 *Receivables*). */
export function useReceivables() {
  return useQuery({
    queryKey: reportKeys.receivables(),
    queryFn: async () => {
      const { data, error } = await api.GET("/api/reports/receivables");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** The membership plans as they stand today; no range (§12 *Operational reports*). */
export function useSubscriptionsSnapshot() {
  return useQuery({
    queryKey: reportKeys.subscriptions(),
    queryFn: async () => {
      const { data, error } = await api.GET("/api/reports/subscriptions");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Who the Owner should call today; no range (§12 *Needs attention*). */
export function useNeedsAttention() {
  return useQuery({
    queryKey: reportKeys.needsAttention(),
    queryFn: async () => {
      const { data, error } = await api.GET("/api/reports/needs-attention");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}
