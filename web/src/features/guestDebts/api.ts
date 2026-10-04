import { keepPreviousData, useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type GuestDebt = components["schemas"]["GuestDebtResponse"];

export const guestDebtsPageSize = 20;

/**
 * One key for the list. The service charge, cafe and guest «تسویه یکجا» mutations invalidate it,
 * because each of them can pay, void or add a guest's purchase.
 */
export const guestDebtKeys = {
  all: ["guestDebts"] as const,
  page: (page: number) => [...guestDebtKeys.all, page] as const,
};

/**
 * «بدهی مهمان‌ها» (BUSINESS_RULES.md §7 *Guest visit*): every cafe order and service charge of a
 * guest's visit that still owes money, newest first, whether the guest is still inside or the
 * nightly job closed their visit.
 */
export function useGuestDebts(page: number) {
  return useQuery({
    queryKey: guestDebtKeys.page(page),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/guest-debts", {
        params: { query: { Page: page, PageSize: guestDebtsPageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / guestDebtsPageSize)),
      };
    },
  });
}
