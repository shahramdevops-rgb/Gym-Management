import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { gymToday } from "@/lib/format";
import { normalizeMoney } from "@/lib/money";
import { normalizeDigits } from "@/lib/normalize";

import { payableLimits, type PayableKind } from "./api";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/** Payable.MaxAmount is 9,999,999,999,999,999.99: sixteen digits before the point, two after. */
const amountLimits = { decimals: 2, integerDigits: 16 } as const;

/**
 * Why a typed amount is refused, in Persian, or null when it is a valid amount. The same checks
 * as an expense's amount — greater than zero, refused rather than rounded — answering to the
 * `Payables.*` codes.
 */
export function payableAmountProblem(text: string): string | null {
  const amount = normalizeMoney(text);

  if (amount === "") {
    return "مبلغ را وارد کنید.";
  }
  if (amount.startsWith("-")) {
    return message("Payables.AmountNotPositive");
  }

  const parts = /^(\d+)(?:\.(\d+))?$/.exec(amount);
  if (parts === null) {
    return "مبلغ باید یک عدد باشد.";
  }

  const [, whole = "", fraction = ""] = parts;
  if (!/[1-9]/.test(whole + fraction)) {
    // "0", "0.00" and "000" are all zero.
    return message("Payables.AmountNotPositive");
  }
  if (fraction.length > amountLimits.decimals) {
    return message("Payables.AmountTooManyDecimals");
  }
  if (whole.replace(/^0+(?=\d)/, "").length > amountLimits.integerDigits) {
    return message("Payables.AmountTooLarge");
  }

  return null;
}

/** A whole number typed in Persian or English digits, or null when it is not one. */
export function wholeNumber(text: string): number | null {
  const digits = normalizeDigits(text).trim();
  return /^\d{1,4}$/.test(digits) ? Number(digits) : null;
}

/**
 * A cheque or an instalment as typed (BUSINESS_RULES.md §9 *Cheques and instalments*). Every field
 * is required; the date has no limit, since an old one can be entered late and one can be a year
 * ahead. An instalment also says «قسط n از N», with 1 ≤ n ≤ N ≤ 360.
 */
export const payableSchema = z
  .object({
    kind: z.enum(["Cheque", "Installment"]),
    amount: z.string().superRefine((text, context) => {
      const problem = payableAmountProblem(text);
      if (problem !== null) {
        context.addIssue({ code: "custom", message: problem });
      }
    }),
    dueDate: z.string().min(1, "تاریخ را وارد کنید."),
    payee: z
      .string()
      .trim()
      .min(1, message("Payables.PayeeRequired"))
      .max(payableLimits.payeeMaxLength, message("Payables.PayeeTooLong")),
    description: z
      .string()
      .trim()
      .min(1, message("Payables.DescriptionRequired"))
      .max(payableLimits.descriptionMaxLength, message("Payables.DescriptionTooLong")),
    categoryId: z.string().min(1, message("Payables.CategoryRequired")),
    installmentNumber: z.string(),
    installmentCount: z.string(),
  })
  .superRefine((values, context) => {
    if (values.kind !== "Installment") {
      return;
    }

    const number = wholeNumber(values.installmentNumber);
    const count = wholeNumber(values.installmentCount);
    if (count === null) {
      context.addIssue({
        code: "custom",
        path: ["installmentCount"],
        message: "تعداد کل قسط‌ها را وارد کنید.",
      });
    } else if (count < 1 || count > payableLimits.maxInstallmentCount) {
      context.addIssue({
        code: "custom",
        path: ["installmentCount"],
        message: message("Payables.InstallmentCountOutOfRange"),
      });
    }
    if (number === null) {
      context.addIssue({
        code: "custom",
        path: ["installmentNumber"],
        message: "شمارهٔ قسط را وارد کنید.",
      });
    } else if (number < 1 || (count !== null && number > count)) {
      context.addIssue({
        code: "custom",
        path: ["installmentNumber"],
        message: message("Payables.InstallmentNumberOutOfRange"),
      });
    }
  });

export type PayableValues = z.infer<typeof payableSchema>;

/** A new one starts with no date: the date on a cheque or an instalment is rarely today. */
export function emptyPayableValues(kind: PayableKind = "Cheque"): PayableValues {
  return {
    kind,
    amount: "",
    dueDate: "",
    payee: "",
    description: "",
    categoryId: "",
    installmentNumber: "",
    installmentCount: "",
  };
}

/** A reason to cancel, or to send a payment back to pending: required, at most 500 characters. */
function reasonSchema(requiredCode: string, tooLongCode: string) {
  return z.object({
    reason: z
      .string()
      .trim()
      .min(1, message(requiredCode))
      .max(payableLimits.reasonMaxLength, message(tooLongCode)),
  });
}

export const cancelPayableSchema = reasonSchema(
  "Payables.CancelReasonRequired",
  "Payables.CancelReasonTooLong",
);

export const revertPayableSchema = reasonSchema(
  "Payables.RevertReasonRequired",
  "Payables.RevertReasonTooLong",
);

/**
 * Whether a pending one can be marked paid today (§9): a cheque on or after its date, since a bank
 * does not pay one early; an instalment at any time.
 */
export function canPayToday(
  kind: PayableKind,
  dueDate: string,
  today: string = gymToday(),
): boolean {
  // ISO dates compare correctly as strings.
  return kind === "Installment" || dueDate <= today;
}
