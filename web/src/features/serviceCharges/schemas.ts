import type { DefaultValues } from "react-hook-form";
import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { normalizeMoney } from "@/lib/money";
import { normalizeDigits } from "@/lib/normalize";

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

/** BUSINESS_RULES.md §7 *Sale at the desk*: the API's ServiceCharge limits. */
export const shopSaleLimits = {
  descriptionMaxLength: 100,
  maxQuantity: 999,
  maxItems: 50,
} as const;

/** One item of a «فروشگاه» sale: its name, how many and the price of one. */
const shopItemSchema = z.object({
  description: z
    .string()
    .trim()
    .min(1, message("ServiceCharges.DescriptionRequired"))
    .max(shopSaleLimits.descriptionMaxLength, message("ServiceCharges.DescriptionTooLong")),
  // Typed as text, so Persian digits are accepted (BUSINESS_RULES.md §13); the ▲/▼ buttons write
  // the same text.
  quantity: z.string().superRefine((text, context) => {
    if (parseSaleQuantity(text) === null) {
      context.addIssue({ code: "custom", message: message("ServiceCharges.QuantityInvalid") });
    }
  }),
  unitPrice: z.string().superRefine((text, context) => {
    const problem = serviceChargeAmountProblem(text);
    if (problem !== null) {
      context.addIssue({ code: "custom", message: problem });
    }
  }),
});

/**
 * A «فروشگاه» sale: one or more items, with no payment. It goes on the member's account and is
 * paid afterwards (decided with the developer, 1405/07/12).
 */
export const shopSaleSchema = z.object({
  items: z.array(shopItemSchema).min(1).max(shopSaleLimits.maxItems),
});

export type ShopSaleValues = z.infer<typeof shopSaleSchema>;

/** A fresh item line: one of it, name and price still to type. */
export const emptyShopItem: ShopSaleValues["items"][number] = {
  description: "",
  quantity: "۱",
  unitPrice: "",
};

export const emptyShopSaleValues: DefaultValues<ShopSaleValues> = { items: [emptyShopItem] };

/** A typed quantity, Persian or English digits, as a whole number from 1 to 999; null otherwise. */
export function parseSaleQuantity(text: string): number | null {
  const normalized = normalizeDigits(text).trim();

  return /^[1-9]\d{0,2}$/.test(normalized) ? Number(normalized) : null;
}

/**
 * The ▲/▼ buttons: one more or one fewer, kept within 1 to 999. Text that is not a quantity yet
 * starts again from 1.
 */
export function stepSaleQuantity(text: string, step: 1 | -1): string {
  const current = parseSaleQuantity(text) ?? 1;
  const next = Math.min(shopSaleLimits.maxQuantity, Math.max(1, current + step));

  return toPersianDigits(next);
}
