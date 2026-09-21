import { z } from "zod";

import { errorMessages } from "@/lib/errors";

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

export const assignSubscriptionSchema = z.object({
  planId: z.string().min(1, message("Subscriptions.PlanRequired")),
});

export type AssignSubscriptionValues = z.infer<typeof assignSubscriptionSchema>;

export const emptyAssignSubscriptionValues: AssignSubscriptionValues = { planId: "" };

export const cancelSubscriptionSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, message("Subscriptions.CancelReasonRequired"))
    .max(cancelReasonMaxLength, message("Subscriptions.CancelReasonTooLong")),
});

export type CancelSubscriptionValues = z.infer<typeof cancelSubscriptionSchema>;
