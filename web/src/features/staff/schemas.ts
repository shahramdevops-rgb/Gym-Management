import { z } from "zod";

import { newPasswordSchema } from "@/features/auth/schemas";

/** The same rules as the API's CreateStaffValidator and UserNamePolicy. */
export const createStaffSchema = z.object({
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
  temporaryPassword: newPasswordSchema,
});

export type CreateStaffValues = z.infer<typeof createStaffSchema>;

export const resetPasswordSchema = z.object({
  temporaryPassword: newPasswordSchema,
});

export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>;
