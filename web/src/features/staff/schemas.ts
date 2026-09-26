import { z } from "zod";

import { containsUserName } from "@/features/auth/password";
import { newPasswordSchema } from "@/features/auth/schemas";

/** The same rules as the API's CreateStaffValidator and UserNamePolicy. */
export const createStaffSchema = z
  .object({
    userName: z
      .string()
      .trim()
      .min(1, "نام کاربری را وارد کنید.")
      .min(3, "نام کاربری باید بین ۳ تا ۵۰ نویسه باشد.")
      .max(50, "نام کاربری باید بین ۳ تا ۵۰ نویسه باشد.")
      .regex(
        /^[A-Za-z0-9\-._@+]+$/,
        "نام کاربری فقط می‌تواند حروف انگلیسی، رقم و نویسه‌های - . _ @ + داشته باشد.",
      ),
    fullName: z
      .string()
      .trim()
      .min(1, "نام و نام خانوادگی را وارد کنید.")
      .max(200, "نام بیش از حد طولانی است."),
    temporaryPassword: newPasswordSchema(),
  })
  // The user name is another field, so this rule is checked on the whole form.
  .refine((values) => !containsUserName(values.temporaryPassword, values.userName), {
    message: "نام کاربری نباید داخل رمز عبور باشد.",
    path: ["temporaryPassword"],
  });

export type CreateStaffValues = z.infer<typeof createStaffSchema>;

/** The staff member's user name adds the "must not contain the user name" rule. */
export function resetPasswordSchema(userName: string) {
  return z.object({
    temporaryPassword: newPasswordSchema(userName),
  });
}

export type ResetPasswordValues = z.infer<ReturnType<typeof resetPasswordSchema>>;
