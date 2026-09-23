import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { normalizeMoney } from "@/lib/money";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/**
 * How a typed amount is spelled is shared with plans and payments, in `lib/money.ts`. What it is
 * *called* when it is refused is not: a هوازی amount answers to `ServiceCharges.*` codes, so this
 * stays beside `amountProblem` in the payments feature rather than merging with it — the same
 * split that kept `priceProblem` separate in task 4.8.
 */
export const serviceChargeLimits = {
  decimals: 2,
  /** ServiceCharge.MaxAmount is 9,999,999,999,999,999.99: sixteen digits before the point. */
  integerDigits: 16,
} as const;

/** Why a typed هوازی amount is refused, in Persian, or null when it is a valid amount. */
export function serviceChargeAmountProblem(text: string): string | null {
  const amount = normalizeMoney(text);

  if (amount === "") {
    return "مبلغ را وارد کنید.";
  }
  if (amount.startsWith("-") || /^0+(\.0*)?$/.test(amount)) {
    return message("ServiceCharges.AmountNotPositive");
  }

  const parts = /^(\d+)(?:\.(\d+))?$/.exec(amount);
  if (parts === null) {
    return "مبلغ باید یک عدد باشد.";
  }

  const [, whole = "", fraction = ""] = parts;
  if (fraction.length > serviceChargeLimits.decimals) {
    return message("ServiceCharges.AmountTooManyDecimals");
  }
  if (whole.replace(/^0+(?=\d)/, "").length > serviceChargeLimits.integerDigits) {
    return message("ServiceCharges.AmountTooLarge");
  }

  return null;
}

export const serviceChargeAmountSchema = z.object({
  amount: z.string().superRefine((text, context) => {
    const problem = serviceChargeAmountProblem(text);
    if (problem !== null) {
      context.addIssue({ code: "custom", message: problem });
    }
  }),
});

export type ServiceChargeAmountValues = z.infer<typeof serviceChargeAmountSchema>;

export const emptyServiceChargeAmountValues: ServiceChargeAmountValues = { amount: "" };

/** BUSINESS_RULES.md §7: a void needs a reason, at most 500 characters. */
export const voidReasonMaxLength = 500;

export const voidServiceChargeSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, message("ServiceCharges.VoidReasonRequired"))
    .max(voidReasonMaxLength, message("ServiceCharges.VoidReasonTooLong")),
});

export type VoidServiceChargeValues = z.infer<typeof voidServiceChargeSchema>;

export const emptyVoidServiceChargeValues: VoidServiceChargeValues = { reason: "" };
