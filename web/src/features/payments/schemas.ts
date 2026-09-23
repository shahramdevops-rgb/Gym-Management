import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { normalizeMoney } from "@/lib/money";

import { paymentMethods } from "./api";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/**
 * How a typed amount is spelled is shared with plans, in `lib/money.ts`. What it is *called*
 * when it is refused is not: an amount here answers to `Payments.*` codes and a plan's price to
 * `Plans.*`, so `amountProblem` and `priceProblem` stay apart.
 */
export const paymentLimits = {
  decimals: 2,
  /** Payment.MaxAmount is 9,999,999,999,999,999.99: sixteen digits before the point. */
  integerDigits: 16,
} as const;

/** Why a typed amount is refused, in Persian, or null when it is a valid amount. */
export function amountProblem(text: string): string | null {
  const amount = normalizeMoney(text);

  if (amount === "") {
    return "مبلغ را وارد کنید.";
  }
  if (amount.startsWith("-") || amount === "0") {
    return message("Payments.AmountNotPositive");
  }

  const parts = /^(\d+)(?:\.(\d+))?$/.exec(amount);
  if (parts === null) {
    return "مبلغ باید یک عدد باشد.";
  }

  const [, whole = "", fraction = ""] = parts;
  if (fraction.length > paymentLimits.decimals) {
    return message("Payments.AmountTooManyDecimals");
  }
  if (whole.replace(/^0+(?=\d)/, "").length > paymentLimits.integerDigits) {
    return message("Payments.AmountTooLarge");
  }

  return null;
}

const amountField = z.string().superRefine((text, context) => {
  const problem = amountProblem(text);
  if (problem !== null) {
    context.addIssue({ code: "custom", message: problem });
  }
});

const methodField = z.enum(paymentMethods);

const referenceNumberField = z.string().max(100, message("Payments.ReferenceNumberTooLong"));

export const registerPaymentSchema = z.object({
  amount: amountField,
  method: methodField,
  referenceNumber: referenceNumberField,
});

export type RegisterPaymentValues = z.infer<typeof registerPaymentSchema>;

export const emptyRegisterPaymentValues: RegisterPaymentValues = {
  amount: "",
  method: "Cash",
  referenceNumber: "",
};

/** BUSINESS_RULES.md §5: a reason is required to refund a payment, at most 500 characters. */
export const refundReasonMaxLength = 500;

export const registerRefundSchema = z.object({
  amount: amountField,
  method: methodField,
  referenceNumber: referenceNumberField,
  reason: z
    .string()
    .trim()
    .min(1, message("Payments.RefundReasonRequired"))
    .max(refundReasonMaxLength, message("Payments.RefundReasonTooLong")),
});

export type RegisterRefundValues = z.infer<typeof registerRefundSchema>;

export const emptyRegisterRefundValues: RegisterRefundValues = {
  amount: "",
  method: "Cash",
  referenceNumber: "",
  reason: "",
};
