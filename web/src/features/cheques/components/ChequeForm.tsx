import { Controller, useForm } from "react-hook-form";

import { FormField, JalaliDateField, MoneyField, TextareaField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

import type { ChequeInput } from "../api";
import { chequeSchema, type ChequeValues } from "../schemas";

interface ChequeFormProps {
  defaultValues: ChequeValues;
  submitLabel: string;
  /** Sends the form; a rejection is a server problem and lands on the form's fields. */
  onSubmit: (input: ChequeInput) => Promise<void>;
  onCancel: () => void;
}

/**
 * A cheque the gym gave, for registering one and for correcting a pending one (BUSINESS_RULES.md
 * §9 *Cheques*). The amount goes through `MoneyField` and the date through the Jalali picker, like
 * every amount and date in the app.
 */
export function ChequeForm({ defaultValues, submitLabel, onSubmit, onCancel }: ChequeFormProps) {
  const form = useForm({ resolver: zodResolver(chequeSchema), defaultValues });

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit({
        amount: normalizeMoney(values.amount),
        dueDate: values.dueDate,
        payee: values.payee,
        description: values.description,
      });
    } catch (problem) {
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="space-y-3" onSubmit={submit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <div className="grid gap-3 sm:grid-cols-3">
        <Controller
          control={form.control}
          name="amount"
          render={({ field }) => (
            <MoneyField
              label="مبلغ (تومان)"
              placeholder="۵۰٬۰۰۰٬۰۰۰"
              error={errors.amount?.message}
              name={field.name}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
            />
          )}
        />
        <Controller
          control={form.control}
          name="dueDate"
          render={({ field }) => (
            <JalaliDateField
              label="تاریخ چک"
              error={errors.dueDate?.message}
              name={field.name}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
            />
          )}
        />
        <FormField
          label="در وجه"
          autoComplete="off"
          error={errors.payee?.message}
          {...form.register("payee")}
        />
      </div>

      <TextareaField
        label="شرح"
        rows={2}
        placeholder="مثلاً قسط دوم تردمیل"
        error={errors.description?.message}
        {...form.register("description")}
      />

      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={isSubmitting}>
          {submitLabel}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </form>
  );
}
