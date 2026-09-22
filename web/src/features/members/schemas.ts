import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { gymToday, isoYearsAgo } from "@/lib/format";

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
/** docs/BUSINESS_RULES.md §2, and Member.MaxAgeYears on the API side. */
export const maxAgeYears = 120;

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
  /**
   * An ISO business date or empty: JalaliDateField never produces anything else. Both rules are
   * the entity's (Members.BirthDateInFuture, Members.BirthDateTooOld), repeated here so the form
   * can answer before the request, with the same Persian text the API's codes map to.
   *
   * ISO dates are fixed-width and zero-padded, so comparing them as strings is comparing them as
   * dates. gymToday() is called per validation, not captured at import: a tab left open
   * overnight must not keep validating against yesterday.
   */
  birthDate: z
    .string()
    .refine((value) => value === "" || value <= gymToday(), message("Members.BirthDateInFuture"))
    .refine(
      (value) => value === "" || value >= isoYearsAgo(gymToday(), maxAgeYears),
      message("Members.BirthDateTooOld"),
    ),
  notes: z.string().trim().max(1000, message("Members.NotesTooLong")),
});

export type MemberValues = z.infer<typeof memberSchema>;

/** docs/BUSINESS_RULES.md §2: a shorter search is refused (Members.SearchTooShort). */
export const searchMinLength = 2;
