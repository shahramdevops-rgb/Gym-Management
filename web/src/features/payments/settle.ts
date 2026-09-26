import { useMutation, useQueryClient } from "@tanstack/react-query";

import { attendanceKeys } from "@/features/attendance/api";
import { cafeKeys } from "@/features/cafe/api";
import { memberKeys } from "@/features/members/api";
import { subscriptionKeys } from "@/features/subscriptions/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

import { paymentKeys, type PaymentMethod } from "./api";

/*
 * Its own file rather than api.ts: it refreshes the cafe's cache, and the cafe's api.ts already
 * imports payments/api.ts for its own payments, so putting this there would make the two import
 * each other.
 */

export type Settlement = components["schemas"]["SettlementResponse"];
export type SettlementItem = components["schemas"]["SettleMemberDebtItem"];

export interface SettleDebtInput {
  memberId: string;
  amount: string;
  method: PaymentMethod;
  referenceNumber: string | null;
  /** The ticked items, each with the figure the desk was shown as owed on it. */
  items: SettlementItem[];
}

/**
 * One amount over several owed items (BUSINESS_RULES.md §5 *Settling several items at once*). The
 * money lands on subscriptions, هوازی charges and cafe orders alike, so every cache that shows
 * any of them is refreshed — the board's rows included.
 *
 * A refusal because the debt changed also refreshes the debt, so the list the desk looks at again
 * is the new one rather than the one that was just refused.
 */
export function useSettleDebt() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ memberId, ...body }: SettleDebtInput): Promise<Settlement> => {
      const { data, error } = await api.POST("/api/members/{memberId}/settlements", {
        params: { path: { memberId } },
        body,
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: paymentKeys.all }),
        queryClient.invalidateQueries({ queryKey: subscriptionKeys.all }),
        queryClient.invalidateQueries({ queryKey: memberKeys.all }),
        queryClient.invalidateQueries({ queryKey: cafeKeys.all }),
        queryClient.invalidateQueries({ queryKey: attendanceKeys.all }),
      ]);
    },
    onError: async (_problem, { memberId }) => {
      await queryClient.invalidateQueries({ queryKey: memberKeys.debt(memberId) });
    },
  });
}
