import { useForm, useWatch } from "react-hook-form";
import { useNavigate } from "react-router";

import { paths } from "@/app/paths";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useChangePassword } from "../api";
import { PasswordChecklist, PasswordField } from "../components/PasswordField";
import { newPasswordCodes } from "../password";
import { changePasswordSchema, normalizePassword, type ChangePasswordValues } from "../schemas";
import { tokenUserName, useSessionState } from "../session";

/**
 * Both the forced first-login change and a voluntary one. The only difference is the sentence
 * at the top: a user with a temporary password, or one set before the current password policy,
 * is told why they are here.
 */
export function ChangePasswordPage() {
  const state = useSessionState();
  const navigate = useNavigate();
  const changePassword = useChangePassword();

  const mustChange = state.status === "signedIn" && state.session.mustChangePassword;
  const userName =
    state.status === "signedIn" ? tokenUserName(state.session.accessToken) : undefined;

  const form = useForm<ChangePasswordValues>({
    resolver: zodResolver(changePasswordSchema(userName)),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await changePassword.mutateAsync({
        currentPassword: normalizePassword(values.currentPassword),
        newPassword: normalizePassword(values.newPassword),
      });
      navigate(paths.home, { replace: true });
    } catch (problem) {
      // These are about one field each, so they appear under it rather than at the top.
      applyServerErrors(problem, form.setError, {
        "Auth.CurrentPasswordIncorrect": "currentPassword",
        ...newPasswordCodes("newPassword"),
      });
    }
  });

  const { errors, isSubmitting } = form.formState;
  const newPassword = useWatch({ control: form.control, name: "newPassword" });

  return (
    <Card className="max-w-md">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          تغییر رمز عبور
        </CardTitle>
        <CardDescription>
          {mustChange
            ? "رمز عبور فعلی شما موقت است یا با قوانین تازهٔ امنیتی جور نیست. برای ادامه، رمز عبور تازه‌ای انتخاب کنید."
            : "رمز عبور تازه‌ای انتخاب کنید."}{" "}
          یک جملهٔ کوتاه انگلیسی، مثل my gym opens at 6، رمز خوب و به‌یادماندنی است.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={onSubmit} noValidate>
          {errors.root?.server !== undefined && (
            <Alert variant="destructive">{errors.root.server.message}</Alert>
          )}

          <PasswordField
            label="رمز عبور فعلی"
            autoComplete="current-password"
            error={errors.currentPassword?.message}
            {...form.register("currentPassword")}
          />

          <PasswordField
            label="رمز عبور جدید"
            autoComplete="new-password"
            error={errors.newPassword?.message}
            {...form.register("newPassword")}
          />
          <PasswordChecklist value={newPassword} userName={userName} />

          <PasswordField
            label="تکرار رمز عبور جدید"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            {...form.register("confirmPassword")}
          />

          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "در حال ذخیره…" : "ذخیرهٔ رمز عبور"}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
