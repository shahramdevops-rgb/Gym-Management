import { z } from "zod";

import { errorMessages } from "@/lib/errors";
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

/** A landline as the API cleans it (ShopContactText.Landline): Latin digits, no spaces or dashes. */
export function cleanLandline(typed: string): string {
  return normalizeDigits(typed).replace(/[\s-]/g, "");
}

/** A messenger id as the API cleans it (ShopContactText.Handle): trimmed, no leading `@`. */
export function cleanHandle(typed: string): string {
  return typed.trim().replace(/^@+/, "");
}

/** The same patterns as the API's ShopContactRules. */
const landlinePattern = /^0[1-8]\d{9}$/;
const handlePattern = /^[A-Za-z0-9_.]{3,32}$/;

const handle = z
  .string()
  .refine(
    (typed) => cleanHandle(typed) === "" || handlePattern.test(cleanHandle(typed)),
    message("Shop.HandleInvalid"),
  );

/**
 * Every way is optional. The landline and the ids are checked here as the API checks them; whether
 * a mobile is a real Iranian one only the server can say (libphonenumber), and the form shows its
 * answer under the field.
 */
export const shopContactsSchema = z.object({
  mobilePhone: z.string(),
  landlinePhone: z
    .string()
    .refine(
      (typed) => cleanLandline(typed) === "" || landlinePattern.test(cleanLandline(typed)),
      message("Shop.LandlineInvalid"),
    ),
  whatsAppPhone: z.string(),
  telegram: handle,
  rubika: handle,
  eitaa: handle,
  bale: handle,
  instagram: handle,
});

export type ShopContactsValues = z.infer<typeof shopContactsSchema>;

/** The same limits as the API's ShopCategory, ShopProduct and ShopVariant. */
export const catalogLimits = {
  categoryName: 40,
  productName: 120,
  description: 2000,
  label: 60,
  variants: 20,
  photos: 8,
  maxStock: 999_999,
} as const;

export const categorySchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, message("ShopCategories.NameRequired"))
    .max(catalogLimits.categoryName, message("ShopCategories.NameTooLong")),
});

export type CategoryValues = z.infer<typeof categorySchema>;

/** A typed price as the API reads it ("1200000"), or why it is refused. */
export function priceProblem(typed: string): string | null {
  const price = normalizeMoney(typed);
  const parts = /^(\d+)(?:\.(\d{1,2}))?$/.exec(price);
  if (parts === null) {
    return price.includes(".")
      ? message("ShopProducts.VariantPriceTooManyDecimals")
      : message("ShopProducts.VariantPriceNotPositive");
  }
  if (Number(price) <= 0) {
    return message("ShopProducts.VariantPriceNotPositive");
  }
  const whole = (parts[1] ?? "").replace(/^0+(?=\d)/, "");
  return whole.length > 16 ? message("ShopProducts.VariantPriceTooLarge") : null;
}

/** Stock typed with Persian or English digits, as a whole number. */
export function stockNumber(typed: string): number | null {
  const digits = normalizeDigits(typed).trim();
  return /^\d{1,6}$/.test(digits) ? Number(digits) : null;
}

const variantSchema = z.object({
  /** Null for a variant the form added; the API then makes it. */
  id: z.string().nullable(),
  label: z.string().max(catalogLimits.label, message("ShopProducts.VariantLabelTooLong")),
  price: z.string().superRefine((typed, context) => {
    const problem = priceProblem(typed);
    if (problem !== null) {
      context.addIssue({ code: "custom", message: problem });
    }
  }),
  stock: z
    .string()
    .refine((typed) => stockNumber(typed) !== null, message("ShopProducts.VariantStockOutOfRange")),
});

/**
 * A product's form (§16 *Products*): the rules across variants checked here as the API checks them,
 * each under the variant it is about, so the shop admin sees which box to fix.
 */
export const productSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1, message("ShopProducts.NameRequired"))
      .max(catalogLimits.productName, message("ShopProducts.NameTooLong")),
    categoryId: z.string().min(1, message("ShopProducts.CategoryRequired")),
    description: z
      .string()
      .max(catalogLimits.description, message("ShopProducts.DescriptionTooLong")),
    isVisible: z.boolean(),
    variants: z
      .array(variantSchema)
      .min(1, message("ShopProducts.VariantsRequired"))
      .max(catalogLimits.variants, message("ShopProducts.TooManyVariants")),
  })
  .superRefine((values, context) => {
    const many = values.variants.length > 1;
    const seen = new Map<string, number>();
    values.variants.forEach((variant, index) => {
      const label = variant.label.trim();
      if (many && label === "") {
        context.addIssue({
          code: "custom",
          path: ["variants", index, "label"],
          message: message("ShopProducts.VariantLabelRequired"),
        });
      }
      const key = label.replace(/ي/g, "ی").replace(/ك/g, "ک").toLowerCase();
      if (label !== "" && seen.has(key)) {
        context.addIssue({
          code: "custom",
          path: ["variants", index, "label"],
          message: message("ShopProducts.VariantLabelDuplicate"),
        });
      }
      seen.set(key, index);
    });
  });

export type ProductValues = z.infer<typeof productSchema>;
