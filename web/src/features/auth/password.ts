import { toPersianDigits } from "@/lib/format";
import { normalizeDigits } from "@/lib/normalize";

/**
 * The password policy as the browser can check it while the user types (docs/BUSINESS_RULES.md
 * §1). A mirror of `PasswordPolicy` in Gym.Domain: the same lengths, the same "English only", the
 * same user-name and sequence checks. The one rule it cannot run is the blocklist of common
 * passwords, which stays on the server; its error arrives under the field after submitting.
 *
 * The API checks everything again. These exist so the checklist can turn green as the user types.
 */
export const passwordMinLength = 12;
export const passwordMaxLength = 128;
const minimumDistinctCharacters = 5;

/**
 * The same runs as the server's `PasswordPolicy.Sequences`. The digit run wraps from 9 to 0,
 * repeated past the longest password allowed.
 */
const sequences = [
  "abcdefghijklmnopqrstuvwxyz",
  "0123456789".repeat(Math.floor(passwordMaxLength / 10) + 2),
  "qwertyuiopasdfghjklzxcvbnm",
  "1qaz2wsx3edc4rfv5tgb6yhn7ujm8ik9ol0p",
  "!@#$%^&*()_+",
];

/**
 * Passwords are sent with English digits. CLAUDE.md: every input accepts Persian and English
 * digits. For a password that only works if the same conversion happens every time one is
 * typed, which it does: every password field in this app goes through this function, and the
 * API converts again. Letters are left exactly as typed.
 */
export function normalizePassword(value: string): string {
  return normalizeDigits(value);
}

/** Printable ASCII only: English letters, digits, symbols and the space. */
export function isEnglishOnly(value: string): boolean {
  return /^[\x20-\x7E]*$/.test(value);
}

/**
 * Something that is not English and not a digit: a Persian letter, most likely because the
 * keyboard is still on Persian. Persian digits do not count, because they are converted.
 */
export function looksLikePersianKeyboard(value: string): boolean {
  return !isEnglishOnly(normalizePassword(value));
}

export function containsUserName(value: string, userName: string | undefined): boolean {
  const name = userName?.trim().toLowerCase() ?? "";
  return name !== "" && value.toLowerCase().includes(name);
}

export function isTooSimple(value: string): boolean {
  const lower = value.toLowerCase();
  if (new Set(lower).size < minimumDistinctCharacters) {
    return true;
  }

  return sequences.some(
    (sequence) => sequence.includes(lower) || [...sequence].reverse().join("").includes(lower),
  );
}

export type PasswordCheckKey = "length" | "english" | "userName" | "simple";

export interface PasswordCheck {
  key: PasswordCheckKey;
  label: string;
  met: boolean;
}

/**
 * The checklist under a new-password field, in the order the user meets them. `userName` is
 * left out when it is not known, and so is its line.
 */
export function passwordChecks(raw: string, userName?: string): PasswordCheck[] {
  const value = normalizePassword(raw);
  const english = isEnglishOnly(value);
  const long = value.length >= passwordMinLength && value.length <= passwordMaxLength;

  const checks: PasswordCheck[] = [
    { key: "length", label: `دست‌کم ${toPersianDigits(passwordMinLength)} نویسه`, met: long },
    {
      key: "english",
      label: "فقط حروف و علامت‌های انگلیسی (کیبورد انگلیسی)",
      met: value !== "" && english,
    },
  ];

  if (userName !== undefined && userName.trim() !== "") {
    checks.push({
      key: "userName",
      label: "نام کاربری داخل رمز نباشد",
      met: value !== "" && !containsUserName(value, userName),
    });
  }

  checks.push({
    key: "simple",
    label: "تکراری یا پشت سر هم نباشد (مثل aaaa یا 1234)",
    met: long && english && !isTooSimple(value),
  });

  return checks;
}

/**
 * The API's password-policy codes, each pointed at the form's new-password field, for
 * `applyServerErrors`. Some of them arrive as a whole-request error (when Identity is the one
 * that refused), and they belong under the box the user is typing in, not above the button.
 */
export function newPasswordCodes<TField extends string>(field: TField): Record<string, TField> {
  return Object.fromEntries(
    [
      "Auth.PasswordTooShort",
      "Auth.PasswordTooLong",
      "Auth.PasswordNotEnglish",
      "Auth.PasswordContainsUserName",
      "Auth.PasswordTooSimple",
      "Auth.PasswordTooCommon",
      "Auth.PasswordUnchanged",
    ].map((code) => [code, field]),
  );
}

/** The first rule a new password breaks, as a Persian sentence, or undefined when it passes. */
export function passwordProblem(raw: string, userName?: string): string | undefined {
  const value = normalizePassword(raw);

  if (value === "") return "رمز عبور را وارد کنید.";
  if (!isEnglishOnly(value))
    return "رمز عبور فقط می‌تواند حروف، رقم و علامت‌های انگلیسی داشته باشد.";
  if (value.length < passwordMinLength) {
    return `رمز عبور باید دست‌کم ${toPersianDigits(passwordMinLength)} نویسه باشد.`;
  }
  if (value.length > passwordMaxLength) return "رمز عبور بیش از حد طولانی است.";
  if (containsUserName(value, userName)) return "نام کاربری نباید داخل رمز عبور باشد.";
  if (isTooSimple(value)) return "رمز عبور تکراری یا پشت سر هم است.";

  return undefined;
}
