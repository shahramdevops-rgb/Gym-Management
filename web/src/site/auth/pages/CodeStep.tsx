import { useEffect, useEffectEvent, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { useNavigate } from "react-router";

import { FormField } from "@/components/InputField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage, errorMessages } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { applyServerErrors, withPersianDigits } from "@/lib/forms";
import { normalizeDigits } from "@/lib/normalize";
import { useNow } from "@/lib/useNow";

import { sitePaths } from "../../paths";
import { useRequestCode, useSignIn } from "../api";
import { signUpTicketStore } from "../signUpTicket";
import { readCodeFromSms } from "../webOtp";
import type { SentCode } from "./SignInPage";

/** BUSINESS_RULES.md §15: the code has 5 digits. */
const codeLength = 5;

function isWholeCode(code: string): boolean {
  return code.length === codeLength && /^\d+$/.test(code);
}

interface CodeValues {
  code: string;
}

/** The failures that are about the code itself, shown under its box rather than above the form. */
const codeErrors = {
  "SiteAuth.CodeWrong": "code",
  "SiteAuth.CodeExpired": "code",
  "SiteAuth.CodeTooManyWrongTries": "code",
} as const;

interface CodeStepProps {
  sent: SentCode;
  onResent: (sent: SentCode) => void;
  onChangePhone: () => void;
}

/**
 * The code sent to the phone. It is sent as soon as its fifth digit is in, whether typed, offered
 * by an iPhone above the keyboard (`autocomplete="one-time-code"`) or read from the SMS by Android
 * (WebOTP), so nobody has to find a button. «ارسال دوباره» waits for the time the API gave.
 */
export function CodeStep({ sent, onResent, onChangePhone }: CodeStepProps) {
  const navigate = useNavigate();
  const signIn = useSignIn();
  const requestCode = useRequestCode();
  const now = useNow(1000);
  const [resendError, setResendError] = useState<string | null>(null);

  const form = useForm<CodeValues>({ defaultValues: { code: "" } });

  const submit = form.handleSubmit(async (values) => {
    const code = normalizeDigits(values.code).trim();
    try {
      const response = await signIn.mutateAsync({ phoneNumber: sent.phoneNumber, code });
      if (response.outcome === "NeedsProfile") {
        signUpTicketStore.set({
          phoneNumber: sent.phoneNumber,
          ticket: response.signUpTicket!,
          expiresAt: response.signUpTicketExpiresAt!,
        });
        navigate(sitePaths.signUp, { replace: true });
      }
      // A signed-in person is sent on by SignInPage, which sees the session change.
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeErrors);
    }
  });

  // Set at once, not read from a render: an iPhone's fill can fire the box's change event twice
  // before React renders again, and a second request would find the code already used.
  const sending = useRef(false);
  const smsWait = useRef<AbortController | null>(null);

  // The fifth digit sends the code, whether typed, filled in by the browser (both fire the box's
  // change event) or read from the SMS.
  const sendWhenComplete = () => {
    if (sending.current || !isWholeCode(normalizeDigits(form.getValues("code")).trim())) {
      return;
    }
    sending.current = true;
    smsWait.current?.abort();
    void submit().finally(() => {
      sending.current = false;
    });
  };

  // Fewer than 5 digits never leave the phone: the API would count them as a wrong try, and 15 in
  // a day close the phone until tomorrow (BUSINESS_RULES.md §15).
  const codeRegistration = withPersianDigits(
    form.register("code", {
      validate: (value) => {
        const code = normalizeDigits(value).trim();
        if (code === "") {
          return errorMessages["SiteAuth.CodeRequired"];
        }
        return isWholeCode(code) || errorMessages["SiteAuth.CodeWrong"];
      },
    }),
  );

  const onCodeFromSms = useEffectEvent((code: string) => {
    form.setValue("code", toPersianDigits(code));
    sendWhenComplete();
  });

  // Android: offered to read the code from the SMS until the box holds a whole code.
  useEffect(() => {
    const stop = new AbortController();
    smsWait.current = stop;
    void readCodeFromSms(stop.signal).then((code) => {
      if (code !== undefined) {
        onCodeFromSms(code);
      }
    });
    return () => stop.abort();
  }, [sent]);

  const resend = async () => {
    setResendError(null);
    try {
      const response = await requestCode.mutateAsync({ phoneNumber: sent.phoneNumber });
      onResent({ phoneNumber: sent.phoneNumber, response });
    } catch (problem) {
      setResendError(errorMessage(problem));
    }
  };

  const secondsToResend = Math.max(
    0,
    Math.ceil((Date.parse(sent.response.sendAgainAt) - now.getTime()) / 1000),
  );

  const { errors, isSubmitting } = form.formState;

  return (
    <Card accent={false} className="shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={1} className="text-xl">
          کد ورود
        </CardTitle>
        <CardDescription>
          <SentMessage sent={sent} now={now} />
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={submit} noValidate>
          {errors.root?.server !== undefined && (
            <Alert variant="destructive">{errors.root.server.message}</Alert>
          )}
          {resendError !== null && <Alert variant="destructive">{resendError}</Alert>}

          <FormField
            label="کد پنج‌رقمی"
            dir="ltr"
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={codeLength}
            autoFocus
            className="text-center text-lg tracking-[0.5em]"
            error={errors.code?.message}
            {...codeRegistration}
            onChange={async (event) => {
              await codeRegistration.onChange(event);
              sendWhenComplete();
            }}
          />

          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? "در حال ورود…" : "ورود"}
          </Button>

          <div className="flex items-center justify-between gap-2">
            <Button type="button" variant="ghost" size="sm" onClick={onChangePhone}>
              تغییر شماره
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              disabled={secondsToResend > 0 || requestCode.isPending}
              onClick={() => void resend()}
            >
              {secondsToResend > 0
                ? `ارسال دوباره (${formatCountdown(secondsToResend)})`
                : "ارسال دوباره"}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

/** What the API did with «دریافت کد» or «ارسال دوباره» (BUSINESS_RULES.md §15). */
function SentMessage({ sent, now }: { sent: SentCode; now: Date }) {
  const phone = (
    <span dir="ltr" className="font-medium text-foreground">
      {toPersianDigits(sent.phoneNumber)}
    </span>
  );
  const waitLine = "اگر پیامک نرسید، دو دقیقه صبر کنید و «ارسال دوباره» را بزنید.";

  switch (sent.response.outcome) {
    case "Sent":
      return (
        <>
          کد ورود به {phone} پیامک شد. {waitLine}
        </>
      );
    case "SentAgain":
      return (
        <>
          همان کد دوباره به {phone} پیامک شد و تا{" "}
          {toPersianDigits(minutesLeft(sent.response.expiresAt, now))} دقیقهٔ دیگر معتبر است.
        </>
      );
    case "AlreadySent":
      return <>کد قبلاً برای شما پیامک شده است. {waitLine}</>;
    case "AlreadyDelivered":
      return <>کد به گوشی شما رسیده است؛ پیامک‌هایتان را ببینید.</>;
  }
}

/** Rounded, not rounded up: `now` ticks once a second, so a fresh hour reads ۶۰, not ۶۱. */
function minutesLeft(expiresAt: string, now: Date): number {
  return Math.max(1, Math.round((Date.parse(expiresAt) - now.getTime()) / 60_000));
}

/** `۱:۰۵`: minutes and seconds, as a phone's own timers show them. */
function formatCountdown(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const rest = String(seconds % 60).padStart(2, "0");
  return toPersianDigits(`${minutes}:${rest}`);
}
