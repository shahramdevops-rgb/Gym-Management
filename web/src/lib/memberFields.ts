import { z } from "zod";

import { errorMessages } from "./errors";
import { gymToday, isoYearsAgo } from "./format";

/**
 * A member's name and birth date as a form checks them (docs/BUSINESS_RULES.md §2): the same
 * limits as the API's MemberRules, with the same Persian messages the API's error codes map to,
 * so a mistake reads the same whichever side caught it. The desk's member form and the site's
 * sign-up (roadmap 15.3) both use these, so the two never drift apart.
 */

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/** docs/BUSINESS_RULES.md §2, and Member.MaxAgeYears on the API side. */
export const maxAgeYears = 120;

export const memberFullName = z
  .string()
  .trim()
  .min(1, message("Members.FullNameRequired"))
  .max(200, message("Members.FullNameTooLong"));

/**
 * An ISO business date or empty: BirthDateField never produces anything else. Empty is refused
 * (Members.BirthDateRequired: every member has one). The range rules are the entity's
 * (Members.BirthDateInFuture, Members.BirthDateTooOld), repeated here so the form can answer
 * before the request, with the same Persian text the API's codes map to.
 *
 * ISO dates are fixed-width and zero-padded, so comparing them as strings is comparing them as
 * dates. gymToday() is called per validation, not captured at import: a tab left open
 * overnight must not keep validating against yesterday.
 */
export const memberBirthDate = z
  .string()
  .min(1, message("Members.BirthDateRequired"))
  .refine((value) => value === "" || value <= gymToday(), message("Members.BirthDateInFuture"))
  .refine(
    (value) => value === "" || value >= isoYearsAgo(gymToday(), maxAgeYears),
    message("Members.BirthDateTooOld"),
  );
