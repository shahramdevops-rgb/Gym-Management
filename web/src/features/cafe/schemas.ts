import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { normalizeMoney } from "@/lib/money";

import { cafeLimits } from "./api";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/** Product.MaxPrice follows Plan.Price: sixteen digits before the point, two after (§8). */
const priceLimits = { decimals: 2, integerDigits: 16 } as const;

/**
 * Why a typed price is refused, in Persian, or null when it is a valid price. The same checks as
 * a plan's price, answering to the `Products.*` codes; zero is allowed, as it is on the API
 * (BUSINESS_RULES.md §8: "never negative").
 */
export function productPriceProblem(text: string): string | null {
  const price = normalizeMoney(text);

  if (price === "") {
    return "قیمت را وارد کنید.";
  }
  if (price.startsWith("-")) {
    return message("Products.PriceNegative");
  }

  const parts = /^(\d+)(?:\.(\d+))?$/.exec(price);
  if (parts === null) {
    return "قیمت باید یک عدد باشد.";
  }

  const [, whole = "", fraction = ""] = parts;
  if (fraction.length > priceLimits.decimals) {
    // Refused, not rounded, exactly like the API.
    return message("Products.PriceTooManyDecimals");
  }
  if (whole.replace(/^0+(?=\d)/, "").length > priceLimits.integerDigits) {
    return message("Products.PriceTooLarge");
  }

  return null;
}

export const productSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, message("Products.NameRequired"))
    .max(cafeLimits.nameMaxLength, message("Products.NameTooLong")),
  categoryId: z.string().min(1, message("Products.CategoryRequired")),
  price: z.string().superRefine((text, context) => {
    const problem = productPriceProblem(text);
    if (problem !== null) {
      context.addIssue({ code: "custom", message: problem });
    }
  }),
});

export type ProductValues = z.infer<typeof productSchema>;

export const emptyProductValues: ProductValues = { name: "", categoryId: "", price: "" };

export const categorySchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, message("ProductCategories.NameRequired"))
    .max(cafeLimits.nameMaxLength, message("ProductCategories.NameTooLong")),
});

export type CategoryValues = z.infer<typeof categorySchema>;

export const cancelOrderSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, message("CafeOrders.CancelReasonRequired"))
    .max(cafeLimits.cancelReasonMaxLength, message("CafeOrders.CancelReasonTooLong")),
});

export type CancelOrderValues = z.infer<typeof cancelOrderSchema>;
