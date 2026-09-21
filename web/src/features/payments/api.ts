import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { subscriptionKeys } from "@/features/subscriptions/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Payment = components["schemas"]["PaymentResponse"];
export type PaymentHistoryItem = components["schemas"]["PaymentHistoryResponse"];
export type PaymentMethod = components["schemas"]["PaymentMethod"];

export const paymentsPageSize = 10;

export const paymentMethods: PaymentMethod[] = ["Cash", "Card", "BankTransfer"];

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  Cash: "نقدی",
  Card: "کارت",
  BankTransfer: "انتقال بانکی",
};

/** Query keys. Every key starts with "payments". */
export const paymentKeys = {
  all: ["payments"] as const,
  memberList: (memberId: string, page: number) =>
    [...paymentKeys.all, "memberList", memberId, page] as const,
};

/** A member's payment history across all of their subscriptions (task 4.5). */
export function useMemberPayments(memberId: string, page: number, { enabled = true } = {}) {
  return useQuery({
    queryKey: paymentKeys.memberList(memberId, page),
    enabled,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/members/{memberId}/payments", {
        params: { path: { memberId }, query: { Page: page, PageSize: paymentsPageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / paymentsPageSize)),
      };
    },
  });
}

/**
 * Registering a payment or a refund changes the subscription's net paid amount and payment
 * status, so both the payments cache and the subscriptions cache (the current-subscription card)
 * are invalidated.
 */
function usePaymentMutation<TArgs>(request: (args: TArgs) => Promise<Payment>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: paymentKeys.all }),
        queryClient.invalidateQueries({ queryKey: subscriptionKeys.all }),
      ]);
    },
  });
}

export interface RegisterPaymentInput {
  subscriptionId: string;
  amount: string;
  method: PaymentMethod;
  referenceNumber: string | null;
}

export function useRegisterPayment() {
  return usePaymentMutation(async ({ subscriptionId, ...body }: RegisterPaymentInput) => {
    const { data, error } = await api.POST("/api/subscriptions/{subscriptionId}/payments", {
      params: { path: { subscriptionId } },
      body,
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export interface RegisterRefundInput extends RegisterPaymentInput {
  reason: string;
}

export function useRegisterRefund() {
  return usePaymentMutation(async ({ subscriptionId, ...body }: RegisterRefundInput) => {
    const { data, error } = await api.POST("/api/subscriptions/{subscriptionId}/refunds", {
      params: { path: { subscriptionId } },
      body,
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
