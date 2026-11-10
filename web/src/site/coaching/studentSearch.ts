import { normalizeDigits, normalizePersianText } from "@/lib/normalize";

const phoneQueryPattern = /^[\d\s+\-()]+$/;

/**
 * Whether a student matches the search of «شاگردان من» (BUSINESS_RULES.md §15 *More for the
 * trainer*, 15.7d): a name in any part, ignoring ي/ك and half-spaces; or digits, Persian or English,
 * anywhere in the phone, written with 0, +98 or neither (`0912`, `۴۵۶۷`, `+98912...`).
 * An empty search matches everyone.
 */
export function matchesStudent(
  student: { fullName: string; phoneNumber: string },
  query: string,
): boolean {
  const text = normalizeDigits(normalizePersianText(query)).toLowerCase();
  if (text === "") {
    return true;
  }

  if (phoneQueryPattern.test(text)) {
    const typed = text.replace(/\D/g, "").replace(/^0+/, "");
    const withoutCountry = typed.length > 10 && typed.startsWith("98") ? typed.slice(2) : typed;
    const phone = student.phoneNumber.replace(/\D/g, "");
    const local = phone.startsWith("98") ? phone.slice(2) : phone;

    return withoutCountry !== "" && local.includes(withoutCountry);
  }

  return normalizePersianText(student.fullName).toLowerCase().includes(text);
}
