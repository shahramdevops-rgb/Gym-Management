import { z } from "zod";

import { normalizePassword, passwordProblem } from "./password";

export { normalizePassword };

/**
 * A new password, checked against the policy the browser can run (`passwordProblem`). The API
 * checks again, including the blocklist of common passwords that only it has.
 *
 * `userName`, when known, adds the "must not contain the user name" rule.
 */
export function newPasswordSchema(userName?: string) {
  return z.string().superRefine((value, context) => {
    const problem = passwordProblem(value, userName);
    if (problem !== undefined) {
      context.addIssue({ code: "custom", message: problem });
    }
  });
}

export const loginSchema = z.object({
  userName: z.string().trim().min(1, "نام کاربری را وارد کنید."),
  password: z.string().min(1, "رمز عبور را وارد کنید."),
});

export type LoginValues = z.infer<typeof loginSchema>;

/** `userName` is the logged-in user's, read from the access token. */
export function changePasswordSchema(userName?: string) {
  return z
    .object({
      currentPassword: z.string().min(1, "رمز عبور فعلی را وارد کنید."),
      newPassword: newPasswordSchema(userName),
      confirmPassword: z.string().min(1, "تکرار رمز عبور را وارد کنید."),
    })
    .refine(
      (values) =>
        normalizePassword(values.newPassword) === normalizePassword(values.confirmPassword),
      {
        message: "تکرار رمز عبور با رمز جدید یکسان نیست.",
        path: ["confirmPassword"],
      },
    );
}

export type ChangePasswordValues = z.infer<ReturnType<typeof changePasswordSchema>>;
