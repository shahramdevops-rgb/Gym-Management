import { useMutation, useQueryClient } from "@tanstack/react-query";

import { attendanceKeys } from "@/features/attendance/api";
import { lockerKeys } from "@/features/lockers/api";
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
 * BUSINESS_RULES.md §7 Gym services. Sauna or massage would be another entry here and in the API's
 * enum, not another screen.
 */
export const serviceChargeKindLabels: Record<ServiceChargeKind, string> = {
  Cardio: "هوازی",
  Miscellaneous: "فروشگاه",
  Analysis: "آنالیز",
  Other: "متفرقه",
};

/**
 * The kinds the desk records as a sale (BUSINESS_RULES.md §7 *Sale at the desk*): فروشگاه with a
 * name, a quantity and a unit price, آنالیز and متفرقه (task 6.5.36) with a price alone. All follow
 * the same rules; the kind only says which source it is filed under.
 */
export type SaleKind = Extract<ServiceChargeKind, "Miscellaneous" | "Analysis" | "Other">;

export function isSaleKind(kind: ServiceChargeKind | null): kind is SaleKind {
  return kind === "Miscellaneous" || kind === "Analysis" || kind === "Other";
}

/**
 * What a charge is, for the lists that name an item: «هوازی», or «فروشگاه: دستکش» for a sale,
 * whose name the desk typed (§7 *Sale at the desk*). `null` kind is a row
 * that is a service charge of unknown kind, which the API never sends but the type allows.
 */
export function serviceChargeLabel(
  kind: ServiceChargeKind | null,
  description: string | null | undefined,
): string {
  if (kind === null) {
    return "خدمات";
  }
  const label = serviceChargeKindLabels[kind];

  return description === null || description === undefined ? label : `${label}: ${description}`;
}

/**
 * There is no query hook here. A charge is never fetched on its own: it arrives attached to the
 * visit it belongs to, in `attendance.serviceCharges`, so the screens that show one already have
 * it. Every mutation therefore invalidates the attendance caches — and the member and payment
 * caches with them, because a charge is money the member owes (BUSINESS_RULES.md §5 Member debt).
 * A guest's charge is money owed on their visit instead (§7 *Guest visit*): their locker's
 * «بدهکار» is refreshed too.
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
        queryClient.invalidateQueries({ queryKey: lockerKeys.all }),
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

export interface ShopSaleItemInput {
  description: string;
  quantity: number;
  unitPrice: string;
}

export interface RecordShopSaleInput {
  attendanceId: string;
  items: ShopSaleItemInput[];
}

/**
 * «فروشگاه» (BUSINESS_RULES.md §7 *Sale at the desk*): one or more items in one request, each its
 * own charge, all on the member's account. آنالیز and متفرقه are a single amount and use
 * `useRecordServiceCharge`, like هوازی.
 */
export function useRecordShopSale() {
  return useServiceChargeMutation(async ({ attendanceId, items }: RecordShopSaleInput) => {
    const { data, error } = await api.POST("/api/attendance/{attendanceId}/service-charges/shop", {
      params: { path: { attendanceId } },
      body: { items },
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
