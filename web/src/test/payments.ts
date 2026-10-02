import { fireEvent, screen, within } from "@testing-library/react";

import type { Payment, PaymentHistoryItem, PaymentMethod } from "@/features/payments/api";

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
  subscriptionPlan: { durationDays: 30, totalSessions: 12, isSingleSession: false },
  serviceKind: null,
  kind: "Payment",
  amount: 400000,
  method: "Cash",
  referenceNumber: null,
  paidAt: "2026-09-05T09:00:00Z",
  receivedByUserId: "0199a000-0000-7000-8000-000000000002",
  reason: null,
  createdAt: "2026-09-05T09:00:00Z",
  settlement: null,
};

/** One page of GET /api/members/{memberId}/payments. */
export function paymentsPage(items: PaymentHistoryItem[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 10, totalCount });
}

/** Picks a payment method: every money form starts with none chosen (BUSINESS_RULES.md §5). */
export function pickMethod(
  scope: HTMLElement,
  method: PaymentMethod = "Cash",
  label = "روش پرداخت",
) {
  fireEvent.change(within(scope).getByLabelText(label), { target: { value: method } });
}

/** Answers "yes" in the "was the money received?" box every payment opens before it is sent. */
export async function confirmMoneyReceived() {
  const dialog = await screen.findByRole("dialog", { name: "آیا پول دریافت شد؟" });
  fireEvent.click(within(dialog).getByRole("button", { name: "بله، پول دریافت شد" }));
}

/** Answers "yes" in the "was the money handed back?" box a refund opens before it is sent. */
export async function confirmMoneyReturned() {
  const dialog = await screen.findByRole("dialog", { name: "آیا پول به عضو برگردانده شد؟" });
  fireEvent.click(within(dialog).getByRole("button", { name: "بله، پول برگردانده شد" }));
}
