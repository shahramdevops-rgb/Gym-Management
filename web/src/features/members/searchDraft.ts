import { normalizeDigits, normalizePersianText } from "@/lib/normalize";

import type { MemberValues } from "./schemas";

/**
 * The API's own test for a phone search (BUSINESS_RULES.md §2): only digits, `+`, spaces, dashes
 * and brackets, with at least one digit.
 */
const phoneLike = /^(?=.*\d)[\d+\s\-()]+$/;

/**
 * Starts a new member's form from what the front desk just searched for, so a person nobody
 * found is not typed in twice. A search of only digits was a phone number; anything else was
 * a name. The value is kept as typed (Persian digits stay Persian); the form normalizes on send.
 */
export function memberDraftFromSearch(search: string): MemberValues {
  const typed = search.trim();
  const isPhone = phoneLike.test(normalizeDigits(typed));

  return {
    fullName: isPhone ? "" : normalizePersianText(typed),
    phoneNumber: isPhone ? typed : "",
    birthDate: "",
    notes: "",
  };
}
