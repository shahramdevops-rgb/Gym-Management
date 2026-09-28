import type { CurrentlyInside } from "./api";

/**
 * When the desk should mention renewing (BUSINESS_RULES.md §7, the "currently inside" board).
 * The same numbers Phase 10's SMS reminders are to be configured with, so the desk and the
 * member's text message do not disagree about what "running out" means.
 */
export const lowSessionsThreshold = 3;
export const expiringDaysThreshold = 5;

/**
 * Whole days from the gym's today until a subscription's last day; negative once it has passed.
 * Both dates are read at noon, so a daylight-saving shift cannot turn a day into 23 or 25 hours
 * and round the wrong way.
 */
export function daysUntil(endDate: string, today: string): number {
  const end = new Date(`${endDate}T12:00:00Z`).getTime();
  const start = new Date(`${today}T12:00:00Z`).getTime();

  return Math.round((end - start) / 86_400_000);
}

/** What is running out on a visit's subscription, for the desk panel's renewal list. */
export type RenewalDue = { kind: "sessions"; left: number } | { kind: "days"; left: number };

/**
 * Whether the desk panel lists this visit under «فرصت تمدید» (BUSINESS_RULES.md §6 *The desk
 * panel*), and why. Sessions are named first when both are running out: a session count is what
 * the member feels at the door.
 *
 * Never for a single visit (spent by design and expiring tonight, §7), and never for a member who
 * has already bought the next subscription: they have renewed.
 */
export function renewalDue(visit: CurrentlyInside, today: string): RenewalDue | null {
  if (visit.isSingleSession || visit.hasQueuedRenewal) {
    return null;
  }

  const sessionsLeft = Number(visit.remainingSessions);
  if (sessionsLeft <= lowSessionsThreshold) {
    return { kind: "sessions", left: sessionsLeft };
  }

  const daysLeft = daysUntil(visit.subscriptionEndDate, today);
  if (daysLeft <= expiringDaysThreshold) {
    return { kind: "days", left: daysLeft };
  }

  return null;
}
