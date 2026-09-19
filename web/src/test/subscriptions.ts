import type { Subscription } from "@/features/subscriptions/api";

import { json } from "./mockApi";

export const activeSubscription: Subscription = {
  id: "0199a000-0000-7000-8000-0000000000c1",
  memberId: "0199a000-0000-7000-8000-0000000000a1",
  planId: "0199a000-0000-7000-8000-0000000000b1",
  planName: "یک ماهه ۱۲ جلسه",
  price: 900000,
  durationDays: 30,
  totalSessions: 12,
  usedSessions: 3,
  remainingSessions: 9,
  startDate: "2026-09-01",
  endDate: "2026-09-30",
  status: "Active",
  frozenSince: null,
  totalFrozenDays: 0,
  cancelledAt: null,
  cancellationReason: null,
  version: 1,
  createdAt: "2026-09-01T06:30:00Z",
  netPaid: 400000,
  paymentStatus: "Partial",
};

/**
 * A renewal of `activeSubscription`, queued to start the day after it ends (BUSINESS_RULES.md
 * §4): a second, later-starting subscription for the same member, used to test that management
 * actions (freeze, cancel, …) still reach whichever row they target, not just the newest one.
 */
export const queuedRenewal: Subscription = {
  ...activeSubscription,
  id: "0199a000-0000-7000-8000-0000000000c2",
  startDate: "2026-10-01",
  endDate: "2026-10-30",
  status: "Upcoming",
  netPaid: 0,
  paymentStatus: "Unpaid",
};

/**
 * A later, cancelled subscription for the same member as `activeSubscription` — the exact shape
 * that exposed the "current" bug: the newest subscription by start date is not the relevant one
 * once it has been cancelled and something older is still active.
 */
export const cancelledRenewal: Subscription = {
  ...activeSubscription,
  id: "0199a000-0000-7000-8000-0000000000c3",
  startDate: "2026-10-01",
  endDate: "2026-10-30",
  status: "Cancelled",
  cancelledAt: "2026-09-20T10:00:00Z",
  cancellationReason: "اشتباه ثبت شد",
  netPaid: 0,
  paymentStatus: "Unpaid",
};

/** One page of GET /api/members/{memberId}/subscriptions. */
export function subscriptionsPage(items: Subscription[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 10, totalCount });
}
