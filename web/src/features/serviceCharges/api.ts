import { useMutation, useQueryClient } from "@tanstack/react-query";

import { attendanceKeys } from "@/features/attendance/api";
import { memberKeys } from "@/features/members/api";
import { paymentKeys } from "@/features/payments/api";
import type { PaymentMethod } from "@/features/payments/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type ServiceCharge = components["schemas"]["ServiceChargeResponse"];
/**
 * `NonNullable` because the generated schema says `"Cardio" | null`: the enum itself has no null
 * member, but it is used as an optional field on the debt breakdown and the payment history, and
 * the generator marks the shared component nullable rather than those two usages. The null belongs
 * to "this row is not a service charge", not to the kind.
 */
export type ServiceChargeKind = NonNullable<components["schemas"]["ServiceChargeKind"]>;

/**
 * BUSINESS_RULES.md §7 Gym services: today there is exactly one kind. Sauna or massage would be
 * another entry here and in the API's enum, not another screen.
 */
export const serviceChargeKindLabels: Record<ServiceChargeKind, string> = {
  Cardio: "هوازی",
};

/**
 * There is no query hook here. A charge is never fetched on its own: it arrives attached to the
 * visit it belongs to, in `attendance.serviceCharges`, so the screens that show one already have
 * it. Every mutation therefore invalidates the attendance caches — and the member and payment
 * caches with them, because a charge is money the member owes (BUSINESS_RULES.md §5 Member debt).
 */
function useServiceChargeMutation<TArgs, TResult>(request: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: attendanceKeys.all }),
        queryClient.invalidateQueries({ queryKey: memberKeys.all }),
        queryClient.invalidateQueries({ queryKey: paymentKeys.all }),
      ]);
    },
  });
}

export interface RecordServiceChargeInput {
  attendanceId: string;
  kind: ServiceChargeKind;
  amount: string;
}

export function useRecordServiceCharge() {
  return useServiceChargeMutation(async ({ attendanceId, ...body }: RecordServiceChargeInput) => {
    const { data, error } = await api.POST("/api/attendance/{attendanceId}/service-charges", {
      params: { path: { attendanceId } },
      body,
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export interface ChangeServiceChargeAmountInput {
  id: string;
  amount: string;
}

export function useChangeServiceChargeAmount() {
  return useServiceChargeMutation(async ({ id, amount }: ChangeServiceChargeAmountInput) => {
    const { data, error } = await api.PUT("/api/service-charges/{id}/amount", {
      params: { path: { id } },
      body: { amount },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export interface VoidServiceChargeInput {
  id: string;
  reason: string;
}

export function useVoidServiceCharge() {
  return useServiceChargeMutation(async ({ id, reason }: VoidServiceChargeInput) => {
    const { data, error } = await api.POST("/api/service-charges/{id}/void", {
      params: { path: { id } },
      body: { reason },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export interface RegisterServiceChargePaymentInput {
  id: string;
  amount: string;
  method: PaymentMethod;
  referenceNumber: string | null;
}

export function useRegisterServiceChargePayment() {
  return useServiceChargeMutation(async ({ id, ...body }: RegisterServiceChargePaymentInput) => {
    const { data, error } = await api.POST("/api/service-charges/{id}/payments", {
      params: { path: { id } },
      body,
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
