import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { gymToday } from "@/lib/format";
import { normalizeMoney } from "@/lib/money";

import { chequeLimits } from "./api";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/** Cheque.MaxAmount is 9,999,999,999,999,999.99: sixteen digits before the point, two after. */
const amountLimits = { decimals: 2, integerDigits: 16 } as const;

/**
 * Why a typed amount is refused, in Persian, or null when it is a valid amount. The same checks
 * as an expense's amount — greater than zero, refused rather than rounded — answering to the
 * `Cheques.*` codes.
 */
export function chequeAmountProblem(text: string): string | null {
  const amount = normalizeMoney(text);

  if (amount === "") {
    return "مبلغ را وارد کنید.";
  }
  if (amount.startsWith("-")) {
    return message("Cheques.AmountNotPositive");
  }

  const parts = /^(\d+)(?:\.(\d+))?$/.exec(amount);
  if (parts === null) {
    return "مبلغ باید یک عدد باشد.";
  }

  const [, whole = "", fraction = ""] = parts;
  if (!/[1-9]/.test(whole + fraction)) {
    // "0", "0.00" and "000" are all zero.
    return message("Cheques.AmountNotPositive");
  }
  if (fraction.length > amountLimits.decimals) {
    return message("Cheques.AmountTooManyDecimals");
  }
  if (whole.replace(/^0+(?=\d)/, "").length > amountLimits.integerDigits) {
    return message("Cheques.AmountTooLarge");
  }

  return null;
}

/**
 * A cheque as typed (BUSINESS_RULES.md §9 *Cheques*). Every field is required; the date has no
 * limit, since an old cheque can be entered late and an instalment can be a year ahead.
 */
export const chequeSchema = z.object({
  amount: z.string().superRefine((text, context) => {
    const problem = chequeAmountProblem(text);
    if (problem !== null) {
      context.addIssue({ code: "custom", message: problem });
    }
  }),
  dueDate: z.string().min(1, "تاریخ چک را وارد کنید."),
  payee: z
    .string()
    .trim()
    .min(1, message("Cheques.PayeeRequired"))
    .max(chequeLimits.payeeMaxLength, message("Cheques.PayeeTooLong")),
  description: z
    .string()
    .trim()
    .min(1, message("Cheques.DescriptionRequired"))
    .max(chequeLimits.descriptionMaxLength, message("Cheques.DescriptionTooLong")),
});

export type ChequeValues = z.infer<typeof chequeSchema>;

/** A new cheque starts with no date: the date on a cheque is rarely today. */
export function emptyChequeValues(): ChequeValues {
  return { amount: "", dueDate: "", payee: "", description: "" };
}

export const cancelChequeSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, message("Cheques.CancelReasonRequired"))
    .max(chequeLimits.cancelReasonMaxLength, message("Cheques.CancelReasonTooLong")),
});

/** Whether a pending cheque can be marked «پاس شد» today: on or after its date (§9). */
export function isDue(dueDate: string, today: string = gymToday()): boolean {
  // ISO dates compare correctly as strings.
  return dueDate <= today;
}
