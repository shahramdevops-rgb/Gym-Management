import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { normalizeDigits } from "@/lib/normalize";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/** BUSINESS_RULES.md §4 Cancel: a reason is required, at most 500 characters. */
export const cancelReasonMaxLength = 500;

/** The same limits as the API's `Subscription.CreateMembership` (BUSINESS_RULES.md §3). */
export const planLimits = {
  minSessionCount: 5,
  /** Nobody trains more than once a day, so a 70-day plan tops out here. */
  maxSessionCount: 140,
} as const;

/**
 * How many days a plan of so many sessions lasts (BUSINESS_RULES.md §3, task 6.5.18), the same
 * table as the API's `Subscription.DurationTable`: the first row whose `maxSessions` is at least
 * the plan's sessions wins.
 */
const durationTable = [
  { maxSessions: 10, days: 30 },
  { maxSessions: 20, days: 45 },
  { maxSessions: planLimits.maxSessionCount, days: 70 },
] as const;

/**
 * The days a plan of `sessions` sessions lasts, or null when that many sessions cannot be sold.
 * Only for showing: the server works the days out again when it sells.
 */
export function planDaysFor(sessions: number): number | null {
  if (sessions < planLimits.minSessionCount) {
    return null;
  }
  return durationTable.find((row) => sessions <= row.maxSessions)?.days ?? null;
}

/** A whole number typed with any digits (`۳۰`, `30`), or null for anything else. */
export function parseWholeNumber(text: string): number | null {
  const normalized = normalizeDigits(text).trim();

  return /^\d{1,6}$/.test(normalized) ? Number(normalized) : null;
}

/**
 * The plan the desk builds for a member: only its sessions, as typed. The text is checked after
 * digit normalization, so `۱۲` is as valid as `12`, and becomes a number only when the form is
 * sent. There is no days field and no price field: both follow from the sessions, and the server
 * works them out (BUSINESS_RULES.md §3).
 */
export const assignSubscriptionSchema = z.object({
  sessionCount: z
    .string()
    .refine((text) => {
      const sessions = parseWholeNumber(text);
      return sessions !== null && sessions >= planLimits.minSessionCount;
    }, message("Subscriptions.SessionCountTooLow"))
    .refine((text) => {
      const sessions = parseWholeNumber(text);
      return sessions === null || sessions <= planLimits.maxSessionCount;
    }, message("Subscriptions.SessionCountTooHigh")),
});

export type AssignSubscriptionValues = z.infer<typeof assignSubscriptionSchema>;

export const emptyAssignSubscriptionValues: AssignSubscriptionValues = {
  sessionCount: "",
};

export const cancelSubscriptionSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, message("Subscriptions.CancelReasonRequired"))
    .max(cancelReasonMaxLength, message("Subscriptions.CancelReasonTooLong")),
});

export type CancelSubscriptionValues = z.infer<typeof cancelSubscriptionSchema>;
