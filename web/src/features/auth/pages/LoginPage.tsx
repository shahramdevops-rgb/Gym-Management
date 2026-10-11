import { useForm } from "react-hook-form";
import { Navigate, useLocation, useNavigate } from "react-router";

import { paths } from "@/app/paths";
import logo from "@/assets/brand/pasargad-logo.png";
import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeDigits } from "@/lib/normalize";

import { useLogin } from "../api";
import { PasswordField } from "../components/PasswordField";
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

  // The panel's look before the panel (roadmap 13.2g): the logo in the menu's blue-to-violet
  // square, a soft glow of the same two colours behind the card, and the card's top edge in them.
  // All of it is decoration; the card's title says where the user is.
  return (
    <div className="isolate flex min-h-screen items-center justify-center overflow-hidden bg-background p-6">
      <div className="relative w-full max-w-sm space-y-6">
        {/* Close behind the card, not in the window's corners, so it shows on any screen size. */}
        <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
          <div className="absolute -start-24 top-0 size-80 rounded-full bg-primary/20 blur-3xl" />
          <div className="absolute -end-24 bottom-0 size-80 rounded-full bg-(--tone-violet)/20 blur-3xl" />
        </div>

        <div className="flex flex-col items-center gap-3 text-center">
          {/* White lines on transparency, so it sits on the brand blue in both themes. */}
          <span
            aria-hidden
            className="flex size-20 items-center justify-center rounded-2xl bg-linear-to-br from-primary to-(--tone-violet) p-2.5 shadow-lg shadow-primary/30"
          >
            <img src={logo} alt="" className="size-full object-contain" />
          </span>
          <p className="text-lg font-extrabold">باشگاه پاسارگاد</p>
        </div>

        <Card accent={false} className="relative overflow-hidden shadow-raised">
          <span
            aria-hidden
            className="absolute inset-x-0 top-0 h-1 bg-linear-to-l from-primary to-(--tone-violet)"
          />
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

              <PasswordField
                label="رمز عبور"
                autoComplete="current-password"
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
    </div>
  );
}
