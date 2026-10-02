import { daysUntil } from "@/features/attendance/renewal";

import type { Subscription } from "./api";

/**
 * Whether a subscription is waiting in line behind another membership of the same member
 * (BUSINESS_RULES.md §4): it has not started, and a live membership ends the day before it starts.
 *
 * Such a subscription's dates are only where it stands today, not a promise. If the plan before it
 * runs out of sessions early, the next check-in starts it that day; if the plan before it is
 * frozen, unfreezing pushes it later. Only its length in days is fixed. An upcoming subscription
 * with nothing right before it (the plan it was queued behind was cancelled) stays where it is, so
 * its dates are real.
 *
 * `others` is what the caller has on hand, usually one page of the history. If the plan before it
 * is not among them, this answers `false` and the screen shows the plain dates, which are still
 * correct for today.
 */
export function isQueuedBehindAnother(subscription: Subscription, others: Subscription[]): boolean {
  if (subscription.status !== "Upcoming" || subscription.isSingleSession) {
    return false;
  }

  return others.some(
    (other) =>
      other.id !== subscription.id &&
      !other.isSingleSession &&
      other.status !== "Cancelled" &&
      daysUntil(other.endDate, subscription.startDate) === -1,
  );
}
