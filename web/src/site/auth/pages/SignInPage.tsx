import { useState } from "react";
import { useForm } from "react-hook-form";
import { Navigate } from "react-router";

import { FormField } from "@/components/InputField";
import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessages } from "@/lib/errors";
import { applyServerErrors, withPersianDigits } from "@/lib/forms";
import { normalizeDigits } from "@/lib/normalize";

import { SiteFrame } from "../../components/SiteFrame";
import { UnreachableNotice } from "../../components/UnreachableNotice";
import { sitePaths } from "../../paths";
import { useRequestCode, type RequestCodeResponse } from "../api";
import { useSiteSessionState } from "../session";
import { signUpTicketStore } from "../signUpTicket";
import { CodeStep } from "./CodeStep";

// Checked with React Hook Form's own rules rather than a Zod schema: Zod is the larger part of a
// download every sign-in waits for, and the API checks the number anyway.
interface PhoneValues {
  phoneNumber: string;
}

/** The phone the code went to, and the API's answer about that code. */
export interface SentCode {
  phoneNumber: string;
  response: RequestCodeResponse;
}

/**
 * Signing in and signing up start the same way (BUSINESS_RULES.md §15): the phone, then the code
 * sent to it. Whether the phone is a member's, a trainer's or new is known only after the code,
 * so nobody is asked first whether they "have an account".
 */
export function SignInPage() {
  const state = useSiteSessionState();
  const [sent, setSent] = useState<SentCode | null>(null);

  if (state.status === "signedIn") {
    return <Navigate to={sitePaths.home} replace />;
  }

  // A reload with a live cookie: no phone form flashes before the home screen.
  if (state.status === "restoring") {
    return (
      <SiteFrame>
        <LoadingState />
      </SiteFrame>
    );
  }

  // The session may be fine: asking for a code now would cost an SMS for nothing.
  if (state.status === "unreachable") {
    return <UnreachableNotice />;
  }

  // Shown once, after the session ended for a reason the person should know.
  const notice = state.status === "signedOut" ? state.notice : undefined;

  return (
    <SiteFrame>
      {notice !== undefined && sent === null && <Alert variant="destructive">{notice}</Alert>}

      {sent === null ? (
        <PhoneStep onSent={setSent} />
      ) : (
        <CodeStep sent={sent} onResent={setSent} onChangePhone={() => setSent(null)} />
      )}
    </SiteFrame>
  );
}

function PhoneStep({ onSent }: { onSent: (sent: SentCode) => void }) {
  const requestCode = useRequestCode();

  const form = useForm<PhoneValues>({ defaultValues: { phoneNumber: "" } });

  const onSubmit = form.handleSubmit(async (values) => {
    const phoneNumber = normalizeDigits(values.phoneNumber).trim();
    try {
      const response = await requestCode.mutateAsync({ phoneNumber });
      signUpTicketStore.clear();
      onSent({ phoneNumber, response });
    } catch (problem) {
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <Card accent={false} className="shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={1} className="text-xl">
          ورود یا ثبت‌نام
        </CardTitle>
        <CardDescription>
          شماره موبایل خود را وارد کنید تا کد ورود برایتان پیامک شود.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={onSubmit} noValidate>
          {errors.root?.server !== undefined && (
            <Alert variant="destructive">{errors.root.server.message}</Alert>
          )}

          <FormField
            label="شماره موبایل"
            dir="ltr"
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            placeholder="۰۹۱۲ ۱۲۳ ۴۵۶۷"
            autoFocus
            error={errors.phoneNumber?.message}
            {...withPersianDigits(
              form.register("phoneNumber", {
                validate: (value) => value.trim() !== "" || errorMessages["Members.PhoneRequired"],
              }),
            )}
          />

          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? "در حال فرستادن…" : "دریافت کد"}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
