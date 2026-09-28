import { useForm, useWatch } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { usePrices } from "@/features/settings/api";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { multiplyMoney } from "@/lib/money";

import {
  assignSubscriptionSchema,
  emptyAssignSubscriptionValues,
  parseWholeNumber,
} from "../schemas";

const codeFields = {
  "Subscriptions.DurationInvalid": "durationDays",
  "Subscriptions.SessionCountTooLow": "sessionCount",
} as const;

/** The plan the desk built: so many days, so many sessions (BUSINESS_RULES.md §3). */
export interface PlanChoice {
  durationDays: number;
  sessionCount: number;
}

interface PlanFormProps {
  /** What the button says: selling from the profile, or selling and letting the member in. */
  submitLabel: string;
  /** Sends the sale. A rejection is a problem from the API; the form shows it where it belongs. */
  onSubmit: (plan: PlanChoice) => Promise<void>;
  onCancel: () => void;
}

/**
 * Days and sessions, with the price shown before the desk confirms. There is no price to type: it
 * is the sessions times the session price the Owner set (BUSINESS_RULES.md §3).
 *
 * The price shown is a preview. The server works the price out again when it sells, from the price
 * list as it is at that moment.
 *
 * Used under the profile's subscription card, and in the check-in box on the locker map, where the
 * same press also lets the member in (roadmap 6.5.7).
 */
export function PlanForm({ submitLabel, onSubmit, onCancel }: PlanFormProps) {
  const prices = usePrices();

  const form = useForm({
    resolver: zodResolver(assignSubscriptionSchema),
    defaultValues: emptyAssignSubscriptionValues,
  });
  const sessionsText = useWatch({ control: form.control, name: "sessionCount" });

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit({
        // The schema has already checked both parse.
        durationDays: parseWholeNumber(values.durationDays)!,
        sessionCount: parseWholeNumber(values.sessionCount)!,
      });
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;
  const sessionPrice = prices.data?.sessionPrice ?? null;
  const priceNotSet = prices.isSuccess && sessionPrice === null;
  const sessions = parseWholeNumber(sessionsText);

  return (
    <form className="space-y-3" onSubmit={submit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}
      {prices.isError && <Alert variant="destructive">{errorMessage(prices.error)}</Alert>}
      {priceNotSet && <Alert>{errorMessage({ code: "Pricing.SessionPriceNotSet" })}</Alert>}

      <div className="flex flex-wrap items-start gap-3">
        <div className="w-40">
          <FormField
            label="تعداد روز"
            dir="ltr"
            inputMode="numeric"
            autoComplete="off"
            placeholder="۳۰"
            error={errors.durationDays?.message}
            {...form.register("durationDays")}
          />
        </div>
        <div className="w-40">
          <FormField
            label="تعداد جلسات"
            dir="ltr"
            inputMode="numeric"
            autoComplete="off"
            placeholder="۱۲"
            error={errors.sessionCount?.message}
            {...form.register("sessionCount")}
          />
        </div>
      </div>

      {sessionPrice !== null && (
        <p className="text-sm" aria-live="polite">
          <span className="text-muted-foreground">قیمت: </span>
          {sessions === null ? (
            <span className="text-muted-foreground">هر جلسه {formatMoney(sessionPrice)}</span>
          ) : (
            <span className="font-medium">
              {toPersianDigits(sessions)} جلسه × {formatMoney(sessionPrice)} ={" "}
              {formatMoney(multiplyMoney(sessionPrice, sessions))}
            </span>
          )}
        </p>
      )}

      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={isSubmitting || !prices.isSuccess || priceNotSet}>
          {isSubmitting ? "در حال ثبت…" : submitLabel}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </form>
  );
}
