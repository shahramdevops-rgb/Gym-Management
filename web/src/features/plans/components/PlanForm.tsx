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

/**
 * Whole-request errors from the API that are really about one field. The single-session codes are
 * deliberately absent: they are about the plan as a whole, so they show in the alert at the top.
 */
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
  /**
   * Offers the "single-session plan" choice. Create only: a plan's kind is set once and never
   * changes (docs/BUSINESS_RULES.md §3), so the edit form shows it but cannot change it.
   */
  kindEditable?: boolean;
  /** There is already a single-session plan, so the choice is shown switched off, with why. */
  singleSessionTaken?: boolean;
}

/** The create and edit form. Values are normalized when sent, never while typing. */
export function PlanForm({
  defaultValues = emptyPlanValues,
  submitLabel,
  submittingLabel,
  onSubmit,
  actions,
  kindEditable = false,
  singleSessionTaken = false,
}: PlanFormProps) {
  const form = useForm<PlanValues>({
    resolver: zodResolver(planSchema),
    defaultValues,
  });
  const unlimitedId = useId();
  const singleSessionId = useId();
  const unlimited = useWatch({ control: form.control, name: "unlimitedSessions" });
  const singleSession = useWatch({ control: form.control, name: "singleSession" });

  const submit = form.handleSubmit(async (values) => {
    const single = values.singleSession;
    try {
      await onSubmit({
        name: normalizePersianText(values.name),
        // One day and one session is the walk-in plan's shape, not something typed. Otherwise
        // the schema has already checked these parse.
        durationDays: single ? 1 : parseWholeNumber(values.durationDays)!,
        sessionCount: single
          ? 1
          : values.unlimitedSessions
            ? null
            : parseWholeNumber(values.sessionCount)!,
        price: normalizeMoney(values.price),
        // Omitted means a membership, which is what the API assumes too.
        ...(kindEditable && single ? { kind: "SingleSession" as const } : {}),
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

      {kindEditable ? (
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <input
              id={singleSessionId}
              type="checkbox"
              className="size-4 accent-primary"
              disabled={singleSessionTaken}
              {...form.register("singleSession", {
                onChange: (event: { target: { checked: boolean } }) => {
                  if (event.target.checked) {
                    form.setValue("durationDays", "۱");
                    form.setValue("sessionCount", "۱");
                    form.setValue("unlimitedSessions", false);
                  }
                },
              })}
            />
            <label htmlFor={singleSessionId} className="text-sm font-medium">
              پلن تک‌جلسه‌ای (ورود آزاد)
            </label>
          </div>
          <p className="text-xs text-muted-foreground">
            {singleSessionTaken
              ? "پلن تک‌جلسه‌ای از قبل وجود دارد؛ برای تغییر نرخ، همان را ویرایش کنید."
              : "نرخ یک بار ورود برای کسی که اشتراک ندارد. همیشه ۱ روز و ۱ جلسه است و فقط یکی از آن وجود دارد."}
          </p>
        </div>
      ) : (
        singleSession && (
          <p className="text-sm text-muted-foreground">
            این پلن تک‌جلسه‌ای است: همیشه ۱ روز و ۱ جلسه. نام و قیمت آن را می‌توانید تغییر دهید.
          </p>
        )
      )}

      <FormField
        label="مدت (روز)"
        dir="ltr"
        inputMode="numeric"
        autoComplete="off"
        placeholder="۳۰"
        disabled={singleSession}
        error={singleSession ? undefined : errors.durationDays?.message}
        {...form.register("durationDays")}
      />

      <div className="space-y-2">
        <div className="flex items-center gap-2">
          <input
            id={unlimitedId}
            type="checkbox"
            className="size-4 accent-primary"
            disabled={singleSession}
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
          disabled={unlimited || singleSession}
          error={unlimited || singleSession ? undefined : errors.sessionCount?.message}
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
