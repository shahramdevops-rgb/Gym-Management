/**
 * Input normalization for Persian text.
 *
 * A Persian keyboard, an Arabic keyboard and a phone number pasted from SMS produce different
 * code points for what a user considers the same character. Everything entering the API is
 * normalized here first, so "۰۹۱۲..." and "0912..." are one phone number and "علي" finds "علی".
 * See docs/BUSINESS_RULES.md section 13.
 *
 * Invisible and look-alike characters are written as \u escapes on purpose: a literal one
 * cannot be seen in review, and ESLint rejects it.
 */

const persianZero = 0x06f0; // ۰ .. ۹
const arabicZero = 0x0660; // ٠ .. ٩

/** Converts Persian (۰-۹) and Arabic (٠-٩) digits to English ones. Other characters pass through. */
export function normalizeDigits(value: string): string {
  let result = "";

  for (const character of value) {
    const code = character.codePointAt(0)!;

    if (code >= persianZero && code <= persianZero + 9) {
      result += String(code - persianZero);
    } else if (code >= arabicZero && code <= arabicZero + 9) {
      result += String(code - arabicZero);
    } else {
      result += character;
    }
  }

  return result;
}

/**
 * Normalizes Persian letters and whitespace (docs/BUSINESS_RULES.md section 13):
 * Arabic ي/ى → Persian ی, Arabic ك → Persian ک, harakat and tatweel removed, zero-width
 * non-joiner treated as a space, other zero-width and direction marks removed, whitespace
 * collapsed and trimmed.
 *
 * Users type "میر‌حسین" (half-space) and "میر حسین" (space) interchangeably, so for search
 * the two must be the same string.
 */
export function normalizePersianText(value: string): string {
  return value
    .replace(/[\u064A\u0649]/g, "\u06CC") // ي, ى → ی
    .replace(/\u0643/g, "\u06A9") // ك → ک
    .replace(/[\u064B-\u0652\u0640]/g, "") // harakat (fathatan .. sukun) and tatweel
    .replace(/\u200C/g, " ") // zero-width non-joiner counts as a space
    .replace(/\u200B|\u200D|\u200E|\u200F|\uFEFF/g, "") // other zero-width and direction marks
    .replace(/\s+/g, " ")
    .trim();
}

/**
 * The full input pipeline: Persian letters normalized and every digit turned English.
 * Use this before validating, searching or sending a value to the API.
 */
export function normalizeInput(value: string): string {
  return normalizeDigits(normalizePersianText(value));
}
