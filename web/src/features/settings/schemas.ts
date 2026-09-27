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

/** PriceList.MaxPrice is 9,999,999,999,999,999.99: sixteen digits before the point, two after (§3). */
const priceLimits = { decimals: 2, integerDigits: 16 } as const;

/**
 * Why a typed price is refused, in Persian, or null when it is a valid price. The same checks as the
 * API's `PriceList.CheckPrice`, answering to the `Pricing.*` codes; zero is allowed, as it is there.
 */
export function priceProblem(text: string): string | null {
  const price = normalizeMoney(text);

  if (price === "") {
    return "قیمت را وارد کنید.";
  }
  if (price.startsWith("-")) {
    return message("Pricing.PriceNegative");
  }

  const parts = /^(\d+)(?:\.(\d+))?$/.exec(price);
  if (parts === null) {
    return "قیمت باید یک عدد باشد.";
  }

  const [, whole = "", fraction = ""] = parts;
  if (fraction.length > priceLimits.decimals) {
    // Refused, not rounded, exactly like the API.
    return message("Pricing.PriceTooManyDecimals");
  }
  if (whole.replace(/^0+(?=\d)/, "").length > priceLimits.integerDigits) {
    return message("Pricing.PriceTooLarge");
  }

  return null;
}

const price = z.string().superRefine((text, context) => {
  const problem = priceProblem(text);
  if (problem !== null) {
    context.addIssue({ code: "custom", message: problem });
  }
});

/** The settings form: both prices, always saved together (BUSINESS_RULES.md §3 *Prices*). */
export const pricesSchema = z.object({
  sessionPrice: price,
  singleVisitPrice: price,
});

export type PricesValues = z.infer<typeof pricesSchema>;
