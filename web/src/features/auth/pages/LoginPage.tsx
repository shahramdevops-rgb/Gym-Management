import { useForm } from "react-hook-form";
import { Navigate, useLocation, useNavigate } from "react-router";

import { paths } from "@/app/paths";
import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeDigits } from "@/lib/normalize";

import { useLogin } from "../api";
import { loginSchema, normalizePassword, type LoginValues } from "../schemas";
import { useSessionState } from "../session";

export function LoginPage() {
  const state = useSessionState();
  const navigate = useNavigate();
  const location = useLocation();
  const login = useLogin();

  const from = (location.state as { from?: string } | null)?.from ?? paths.home;

  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { userName: "", password: "" },
  });

  // Someone already logged in who lands here (a bookmark, the back button) goes on to the app.
  if (state.status === "signedIn") {
    return <Navigate to={state.session.mustChangePassword ? paths.changePassword : from} replace />;
  }

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      const session = await login.mutateAsync({
        userName: normalizeDigits(values.userName).trim(),
        password: normalizePassword(values.password),
      });
      navigate(session.mustChangePassword ? paths.changePassword : from, { replace: true });
    } catch (problem) {
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/40 p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle role="heading" aria-level={1} className="text-xl">
            ورود به مدیریت باشگاه
          </CardTitle>
          <CardDescription>نام کاربری و رمز عبور خود را وارد کنید.</CardDescription>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={onSubmit} noValidate>
            {errors.root?.server !== undefined && (
              <Alert variant="destructive">{errors.root.server.message}</Alert>
            )}

            <FormField
              label="نام کاربری"
              autoComplete="username"
              dir="ltr"
              autoFocus
              error={errors.userName?.message}
              {...form.register("userName")}
            />

            <FormField
              label="رمز عبور"
              type="password"
              autoComplete="current-password"
              dir="ltr"
              error={errors.password?.message}
              {...form.register("password")}
            />

            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? "در حال ورود…" : "ورود"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
