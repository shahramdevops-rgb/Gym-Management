import type { Payment, PaymentHistoryItem } from "@/features/payments/api";

import { json } from "./mockApi";
import { activeSubscription } from "./subscriptions";

export const paymentOfActiveSubscription: Payment = {
  id: "0199a000-0000-7000-8000-0000000000d1",
  subscriptionId: activeSubscription.id,
  serviceChargeId: null,
  kind: "Payment",
  amount: 400000,
  method: "Cash",
  referenceNumber: null,
  paidAt: "2026-09-05T09:00:00Z",
  receivedByUserId: "0199a000-0000-7000-8000-000000000002",
  reason: null,
  targetNetPaid: 400000,
  targetPaymentStatus: "Partial",
  createdAt: "2026-09-05T09:00:00Z",
};

export const paymentHistoryItem: PaymentHistoryItem = {
  id: paymentOfActiveSubscription.id,
  targetKind: "Subscription",
  targetId: activeSubscription.id,
  subscriptionPlanName: activeSubscription.planName,
  serviceKind: null,
  kind: "Payment",
  amount: 400000,
  method: "Cash",
  referenceNumber: null,
  paidAt: "2026-09-05T09:00:00Z",
  receivedByUserId: "0199a000-0000-7000-8000-000000000002",
  reason: null,
  createdAt: "2026-09-05T09:00:00Z",
};

/** One page of GET /api/members/{memberId}/payments. */
export function paymentsPage(items: PaymentHistoryItem[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 10, totalCount });
}
