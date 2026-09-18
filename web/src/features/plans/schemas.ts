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

/** The same limits as the API's Plan entity (docs/BUSINESS_RULES.md §3). */
export const planLimits = {
  nameMaxLength: 100,
  maxDurationDays: 365,
  maxSessionCount: 365,
  priceDecimals: 2,
  /** Plan.MaxPrice is 9,999,999,999,999,999.99: sixteen digits before the point. */
  priceIntegerDigits: 16,
} as const;

/** A whole number typed with any digits (`۳۰`, `30`), or null for anything else. */
export function parseWholeNumber(text: string): number | null {
  const normalized = normalizeDigits(text).trim();

  return /^\d{1,6}$/.test(normalized) ? Number(normalized) : null;
}

/**
 * A price as typed (`۱٬۵۰۰٬۰۰۰`, `1,500,000.50`) turned into the plain decimal string the API
 * reads (`1500000.50`): English digits, no thousands separators, `.` as the decimal point.
 *
 * It stays a string on purpose. A JavaScript number cannot hold every allowed price exactly,
 * and rounding a price is never acceptable (docs/BUSINESS_RULES.md §3).
 */
export function normalizePrice(text: string): string {
  return normalizeDigits(text)
    .replace(/[\s,٬]/g, "") // spaces, commas and the Persian thousands separator ٬
    .replace(/٫/g, "."); // the Persian decimal separator ٫
}

/** Why a typed price is refused, in Persian, or null when it is a valid price. */
export function priceProblem(text: string): string | null {
  const price = normalizePrice(text);

  if (price === "") {
    return "قیمت را وارد کنید.";
  }
  if (price.startsWith("-")) {
    return message("Plans.PriceNegative");
  }

  const parts = /^(\d+)(?:\.(\d+))?$/.exec(price);
  if (parts === null) {
    return "قیمت باید یک عدد باشد.";
  }

  const [, whole = "", fraction = ""] = parts;
  if (fraction.length > planLimits.priceDecimals) {
    // Refused, not rounded, exactly like the API (Plans.PriceTooManyDecimals).
    return message("Plans.PriceTooManyDecimals");
  }
  if (whole.replace(/^0+(?=\d)/, "").length > planLimits.priceIntegerDigits) {
    return message("Plans.PriceTooLarge");
  }

  return null;
}

function isValidSessionCount(text: string): boolean {
  const count = parseWholeNumber(text);

  return count !== null && count >= 1 && count <= planLimits.maxSessionCount;
}

/**
 * The plan form. Number fields hold the text as typed and are checked after digit
 * normalization, so `۳۰` is as valid as `30`; they become numbers only when the form is sent.
 *
 * "Unlimited" is its own checkbox. The API models it as `sessionCount: null`, but an empty box
 * that silently means "unlimited" is easy to leave empty by mistake.
 */
export const planSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1, message("Plans.NameRequired"))
      .max(planLimits.nameMaxLength, message("Plans.NameTooLong")),
    durationDays: z.string().refine((text) => {
      const days = parseWholeNumber(text);
      return days !== null && days >= 1 && days <= planLimits.maxDurationDays;
    }, message("Plans.DurationInvalid")),
    unlimitedSessions: z.boolean(),
    sessionCount: z.string(),
    price: z.string().superRefine((text, context) => {
      const problem = priceProblem(text);
      if (problem !== null) {
        context.addIssue({ code: "custom", message: problem });
      }
    }),
  })
  .refine((values) => values.unlimitedSessions || isValidSessionCount(values.sessionCount), {
    path: ["sessionCount"],
    message: message("Plans.SessionCountInvalid"),
    // Zod skips an object's refinements while any field has an issue. This one only reads two
    // fields that are always strings and booleans, so it runs anyway, and the session count
    // error shows together with the others instead of after they are fixed.
    when: () => true,
  });

export type PlanValues = z.infer<typeof planSchema>;

/** A blank form for a new plan. */
export const emptyPlanValues: PlanValues = {
  name: "",
  durationDays: "",
  unlimitedSessions: false,
  sessionCount: "",
  price: "",
};
