import { z } from "zod";

import { normalizeDigits } from "@/lib/normalize";

/**
 * Client-side checks mirror the API's (PasswordPolicy in Gym.Application) so the user hears
 * about a short password before a round trip. The API still checks everything; these only
 * make the common mistakes faster to fix.
 */
export const passwordMinLength = 8;

// Any script's letters and digits count, so a password typed on a Persian keyboard is fine.
const hasLetter = (value: string) => /\p{L}/u.test(value);
const hasDigit = (value: string) => /\p{Nd}/u.test(value);

export const newPasswordSchema = z
  .string()
  .min(1, "رمز عبور را وارد کنید.")
  .min(passwordMinLength, "رمز عبور باید دست‌کم ۸ نویسه باشد.")
  .refine(
    (value) => hasLetter(value) && hasDigit(value),
    "رمز عبور باید دست‌کم یک حرف و یک رقم داشته باشد.",
  );

export const loginSchema = z.object({
  userName: z.string().trim().min(1, "نام کاربری را وارد کنید."),
  password: z.string().min(1, "رمز عبور را وارد کنید."),
});

export type LoginValues = z.infer<typeof loginSchema>;

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, "رمز عبور فعلی را وارد کنید."),
    newPassword: newPasswordSchema,
    confirmPassword: z.string().min(1, "تکرار رمز عبور را وارد کنید."),
  })
  .refine(
    (values) => normalizePassword(values.newPassword) === normalizePassword(values.confirmPassword),
    {
      message: "تکرار رمز عبور با رمز جدید یکسان نیست.",
      path: ["confirmPassword"],
    },
  );

export type ChangePasswordValues = z.infer<typeof changePasswordSchema>;

/**
 * Passwords are sent with English digits. CLAUDE.md: every input accepts Persian and English
 * digits. For a password that only works if the same conversion happens every time one is
 * typed, which it does: every password field in this app goes through this function, so
 * "رمز۱۲۳۴" and "رمز1234" are the same password. Letters are left exactly as typed.
 */
export function normalizePassword(value: string): string {
  return normalizeDigits(value);
}
