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
  maxDurationDays: 365,
  /** At least 5 sessions; there is no upper limit. */
  minSessionCount: 5,
} as const;

/** A whole number typed with any digits (`۳۰`, `30`), or null for anything else. */
export function parseWholeNumber(text: string): number | null {
  const normalized = normalizeDigits(text).trim();

  return /^\d{1,6}$/.test(normalized) ? Number(normalized) : null;
}

/**
 * The plan the desk builds for a member: days and sessions, as typed. Both hold the text as typed
 * and are checked after digit normalization, so `۳۰` is as valid as `30`; they become numbers only
 * when the form is sent. There is no price field: the price is sessions × the session price, and
 * the server works it out (BUSINESS_RULES.md §3).
 */
export const assignSubscriptionSchema = z.object({
  durationDays: z.string().refine((text) => {
    const days = parseWholeNumber(text);
    return days !== null && days >= 1 && days <= planLimits.maxDurationDays;
  }, message("Subscriptions.DurationInvalid")),
  sessionCount: z.string().refine((text) => {
    const sessions = parseWholeNumber(text);
    return sessions !== null && sessions >= planLimits.minSessionCount;
  }, message("Subscriptions.SessionCountTooLow")),
});

export type AssignSubscriptionValues = z.infer<typeof assignSubscriptionSchema>;

export const emptyAssignSubscriptionValues: AssignSubscriptionValues = {
  durationDays: "",
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
