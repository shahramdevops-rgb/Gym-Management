import { Controller, useForm } from "react-hook-form";
import { Link, Navigate } from "react-router";
import { z } from "zod";

import { BirthDateField, FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { toPersianDigits } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { memberBirthDate, memberFullName } from "@/lib/memberFields";
import { normalizePersianText } from "@/lib/normalize";

import { SiteFrame } from "../../components/SiteFrame";
import { sitePaths } from "../../paths";
import { useSignUp } from "../api";
import { useSiteSessionState } from "../session";
import { signUpTicketStore } from "../signUpTicket";

/** §2's rules for a member's name and birth date, the same the desk's form checks. */
const signUpSchema = z.object({ fullName: memberFullName, birthDate: memberBirthDate });

type SignUpValues = z.infer<typeof signUpSchema>;

/**
 * A phone that is neither a member's nor a trainer's, right after its code (BUSINESS_RULES.md §15):
 * the name and birth date make it a member of the gym, active and with no plan. The phone is the
 * one the code proved; it cannot be changed here.
 */
export function SignUpPage() {
  const state = useSiteSessionState();
  const signUp = useSignUp();
  const ticket = signUpTicketStore.get();

  const form = useForm<SignUpValues>({
    resolver: zodResolver(signUpSchema),
    defaultValues: { fullName: "", birthDate: "" },
  });

  if (state.status === "signedIn") {
    return <Navigate to={sitePaths.home} replace />;
  }

  // Opened without a code just before it (a reload, a bookmark): the phone is proved first.
  if (ticket === undefined) {
    return <Navigate to={sitePaths.signIn} replace />;
  }

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await signUp.mutateAsync({
        ticket: ticket.ticket,
        fullName: normalizePersianText(values.fullName).trim(),
        birthDate: values.birthDate,
      });
      signUpTicketStore.clear();
    } catch (problem) {
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors, isSubmitting } = form.formState;
  const expired = signUp.error !== null && hasCode(signUp.error, "SiteAuth.SignUpExpired");

  return (
    <SiteFrame>
      <Card accent={false} className="shadow-raised">
        <CardHeader>
          <CardTitle role="heading" aria-level={1} className="text-xl">
            ثبت‌نام
          </CardTitle>
          <CardDescription>
            شماره{" "}
            <span dir="ltr" className="font-medium text-foreground">
              {toPersianDigits(ticket.phoneNumber)}
            </span>{" "}
            هنوز در باشگاه ثبت نشده است. برای ثبت‌نام، نام و تاریخ تولد خود را وارد کنید.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={onSubmit} noValidate>
            {errors.root?.server !== undefined && (
              <Alert variant="destructive">{errors.root.server.message}</Alert>
            )}

            <FormField
              label="نام و نام خانوادگی"
              autoComplete="name"
              autoFocus
              error={errors.fullName?.message}
              {...form.register("fullName")}
            />
            <Controller
              control={form.control}
              name="birthDate"
              render={({ field }) => (
                <BirthDateField
                  label="تاریخ تولد"
                  error={errors.birthDate?.message}
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                />
              )}
            />

            {expired ? (
              <Button asChild className="w-full">
                <Link to={sitePaths.signIn} replace>
                  دریافت کد تازه
                </Link>
              </Button>
            ) : (
              <Button type="submit" className="w-full" disabled={isSubmitting}>
                {isSubmitting ? "در حال ثبت‌نام…" : "ثبت‌نام"}
              </Button>
            )}
          </form>
        </CardContent>
      </Card>
    </SiteFrame>
  );
}

function hasCode(problem: unknown, code: string): boolean {
  return (
    typeof problem === "object" && problem !== null && "code" in problem && problem.code === code
  );
}
