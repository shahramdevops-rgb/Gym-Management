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
