import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { gymToday } from "@/lib/format";
import { normalizeMoney } from "@/lib/money";

import { expenseLimits } from "./api";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/** Expense.MaxAmount is 9,999,999,999,999,999.99: sixteen digits before the point, two after. */
const amountLimits = { decimals: 2, integerDigits: 16 } as const;

/**
 * Why a typed amount is refused, in Persian, or null when it is a valid amount. The same checks
 * as a payment's amount — greater than zero, refused rather than rounded (BUSINESS_RULES.md §9) —
 * answering to the `Expenses.*` codes.
 */
export function expenseAmountProblem(text: string): string | null {
  const amount = normalizeMoney(text);

  if (amount === "") {
    return "مبلغ را وارد کنید.";
  }
  if (amount.startsWith("-")) {
    return message("Expenses.AmountNotPositive");
  }

  const parts = /^(\d+)(?:\.(\d+))?$/.exec(amount);
  if (parts === null) {
    return "مبلغ باید یک عدد باشد.";
  }

  const [, whole = "", fraction = ""] = parts;
  if (!/[1-9]/.test(whole + fraction)) {
    // "0", "0.00" and "000" are all zero.
    return message("Expenses.AmountNotPositive");
  }
  if (fraction.length > amountLimits.decimals) {
    return message("Expenses.AmountTooManyDecimals");
  }
  if (whole.replace(/^0+(?=\d)/, "").length > amountLimits.integerDigits) {
    return message("Expenses.AmountTooLarge");
  }

  return null;
}

/**
 * An expense as typed. The date is checked against the gym's today here as well as on the API
 * (`Expenses.DateInFuture`), so a slip of the calendar is caught before anything is sent. ISO
 * dates compare correctly as strings.
 */
export const expenseSchema = z.object({
  amount: z.string().superRefine((text, context) => {
    const problem = expenseAmountProblem(text);
    if (problem !== null) {
      context.addIssue({ code: "custom", message: problem });
    }
  }),
  categoryId: z.string().min(1, message("Expenses.CategoryRequired")),
  expenseDate: z
    .string()
    .min(1, "تاریخ هزینه را وارد کنید.")
    .refine((iso) => iso <= gymToday(), message("Expenses.DateInFuture")),
  description: z
    .string()
    .trim()
    .min(1, message("Expenses.DescriptionRequired"))
    .max(expenseLimits.descriptionMaxLength, message("Expenses.DescriptionTooLong")),
  referenceNumber: z
    .string()
    .trim()
    .max(expenseLimits.referenceNumberMaxLength, message("Expenses.ReferenceNumberTooLong")),
});

export type ExpenseValues = z.infer<typeof expenseSchema>;

/** A new expense starts on today, the usual case: the bill is entered the day it is paid. */
export function emptyExpenseValues(): ExpenseValues {
  return {
    amount: "",
    categoryId: "",
    expenseDate: gymToday(),
    description: "",
    referenceNumber: "",
  };
}

export const voidExpenseSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, message("Expenses.VoidReasonRequired"))
    .max(expenseLimits.voidReasonMaxLength, message("Expenses.VoidReasonTooLong")),
});

export const expenseCategorySchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, message("ExpenseCategories.NameRequired"))
    .max(expenseLimits.categoryNameMaxLength, message("ExpenseCategories.NameTooLong")),
});

export type ExpenseCategoryValues = z.infer<typeof expenseCategorySchema>;
