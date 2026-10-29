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
 * The same limits as the API's TrainerRules and the member's phone check (BUSINESS_RULES.md §15).
 * Whether the phone is a real Iranian mobile, and whether another trainer has it, only the server
 * can say; the form shows those answers under the phone field.
 */
export const trainerSchema = z.object({
  fullName: z
    .string()
    .trim()
    .min(1, message("Trainers.FullNameRequired"))
    .max(200, message("Trainers.FullNameTooLong")),
  phoneNumber: z
    .string()
    .trim()
    .min(1, message("Members.PhoneRequired"))
    .max(30, message("Members.PhoneInvalid")),
});

export type TrainerValues = z.infer<typeof trainerSchema>;
