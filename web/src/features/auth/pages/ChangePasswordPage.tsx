import { useForm } from "react-hook-form";
import { useNavigate } from "react-router";

import { paths } from "@/app/paths";
import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useChangePassword } from "../api";
import { changePasswordSchema, normalizePassword, type ChangePasswordValues } from "../schemas";
import { useSessionState } from "../session";

/**
 * Both the forced first-login change and a voluntary one. The only difference is the sentence
 * at the top: a user with a temporary password is told why they are here.
 */
export function ChangePasswordPage() {
  const state = useSessionState();
  const navigate = useNavigate();
  const changePassword = useChangePassword();

  const mustChange = state.status === "signedIn" && state.session.mustChangePassword;

  const form = useForm<ChangePasswordValues>({
    resolver: zodResolver(changePasswordSchema),
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
      // These two are about one field each, so they appear under it rather than at the top.
      applyServerErrors(problem, form.setError, {
        "Auth.CurrentPasswordIncorrect": "currentPassword",
        "Auth.PasswordUnchanged": "newPassword",
      });
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <Card className="max-w-md">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          تغییر رمز عبور
        </CardTitle>
        <CardDescription>
          {mustChange
            ? "رمز عبور فعلی شما موقت است. برای ادامه، رمز عبور تازه‌ای انتخاب کنید."
            : "رمز عبور تازه دست‌کم ۸ نویسه و شامل حرف و رقم باشد."}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={onSubmit} noValidate>
          {errors.root?.server !== undefined && (
            <Alert variant="destructive">{errors.root.server.message}</Alert>
          )}

          <FormField
            label="رمز عبور فعلی"
            type="password"
            autoComplete="current-password"
            dir="ltr"
            error={errors.currentPassword?.message}
            {...form.register("currentPassword")}
          />

          <FormField
            label="رمز عبور جدید"
            type="password"
            autoComplete="new-password"
            dir="ltr"
            error={errors.newPassword?.message}
            {...form.register("newPassword")}
          />

          <FormField
            label="تکرار رمز عبور جدید"
            type="password"
            autoComplete="new-password"
            dir="ltr"
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
