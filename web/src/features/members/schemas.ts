import { z } from "zod";

import { errorMessages } from "@/lib/errors";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/**
 * The same limits as the API's MemberRules (docs/BUSINESS_RULES.md §2), with the same Persian
 * messages the API's error codes map to, so a mistake reads the same whichever side caught it.
 *
 * Whether a phone number is a real Iranian mobile, and whether another member already has it,
 * only the server can say (libphonenumber, the unique index). The form shows those answers
 * under the phone field.
 */
export const memberSchema = z.object({
  fullName: z
    .string()
    .trim()
    .min(1, message("Members.FullNameRequired"))
    .max(200, message("Members.FullNameTooLong")),
  phoneNumber: z
    .string()
    .trim()
    .min(1, message("Members.PhoneRequired"))
    .max(30, message("Members.PhoneInvalid")),
  notes: z.string().trim().max(1000, message("Members.NotesTooLong")),
});

export type MemberValues = z.infer<typeof memberSchema>;

/** docs/BUSINESS_RULES.md §2: a shorter search is refused (Members.SearchTooShort). */
export const searchMinLength = 2;
