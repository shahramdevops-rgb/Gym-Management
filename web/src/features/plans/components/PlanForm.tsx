import { useId, type ReactNode } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";

import { FormField, MoneyField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";
import { normalizePersianText } from "@/lib/normalize";

import type { PlanInput } from "../api";
import { emptyPlanValues, parseWholeNumber, planSchema, type PlanValues } from "../schemas";

/** Whole-request errors from the API that are really about one field. */
const codeFields = {
  "Plans.NameAlreadyExists": "name",
  "Plans.NameRequired": "name",
  "Plans.NameTooLong": "name",
  "Plans.DurationInvalid": "durationDays",
  "Plans.SessionCountInvalid": "sessionCount",
  "Plans.PriceNegative": "price",
  "Plans.PriceTooLarge": "price",
  "Plans.PriceTooManyDecimals": "price",
} as const;

interface PlanFormProps {
  defaultValues?: PlanValues;
  submitLabel: string;
  submittingLabel: string;
  /**
   * Sends the values. A thrown API problem is shown on the form; a caller that handles an
   * error itself (such as a concurrent edit) returns normally instead.
   */
  onSubmit: (input: PlanInput) => Promise<void>;
  /** Extra buttons beside submit, such as "cancel". */
  actions?: ReactNode;
}

/** The create and edit form. Values are normalized when sent, never while typing. */
export function PlanForm({
  defaultValues = emptyPlanValues,
  submitLabel,
  submittingLabel,
  onSubmit,
  actions,
}: PlanFormProps) {
  const form = useForm<PlanValues>({
    resolver: zodResolver(planSchema),
    defaultValues,
  });
  const unlimitedId = useId();
  const unlimited = useWatch({ control: form.control, name: "unlimitedSessions" });

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit({
        name: normalizePersianText(values.name),
        // The schema has already checked these parse.
        durationDays: parseWholeNumber(values.durationDays)!,
        sessionCount: values.unlimitedSessions ? null : parseWholeNumber(values.sessionCount)!,
        price: normalizeMoney(values.price),
      });
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="grid max-w-xl gap-4" onSubmit={submit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <FormField
        label="نام پلن"
        autoComplete="off"
        error={errors.name?.message}
        {...form.register("name")}
      />
      <FormField
        label="مدت (روز)"
        dir="ltr"
        inputMode="numeric"
        autoComplete="off"
        placeholder="۳۰"
        error={errors.durationDays?.message}
        {...form.register("durationDays")}
      />

      <div className="space-y-2">
        <div className="flex items-center gap-2">
          <input
            id={unlimitedId}
            type="checkbox"
            className="size-4 accent-primary"
            {...form.register("unlimitedSessions")}
          />
          <label htmlFor={unlimitedId} className="text-sm font-medium">
            تعداد جلسات نامحدود
          </label>
        </div>
        {/* Disabled, not removed, so its text comes back if the box is unticked again. */}
        <FormField
          label="تعداد جلسات"
          dir="ltr"
          inputMode="numeric"
          autoComplete="off"
          placeholder={unlimited ? "نامحدود" : "۱۲"}
          disabled={unlimited}
          error={unlimited ? undefined : errors.sessionCount?.message}
          {...form.register("sessionCount")}
        />
      </div>

      <Controller
        control={form.control}
        name="price"
        render={({ field }) => (
          <MoneyField
            label="قیمت (تومان)"
            placeholder="۱٬۵۰۰٬۰۰۰"
            error={errors.price?.message}
            name={field.name}
            value={field.value}
            onChange={field.onChange}
            onBlur={field.onBlur}
          />
        )}
      />

      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? submittingLabel : submitLabel}
        </Button>
        {actions}
      </div>
    </form>
  );
}
