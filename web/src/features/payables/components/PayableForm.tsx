import { Controller, useForm, useWatch } from "react-hook-form";

import {
  FormField,
  JalaliDateField,
  MoneyField,
  SelectField,
  TextareaField,
} from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { ExpenseCategory } from "@/features/expenses/api";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

import { payableKindLabels, payableKinds, type PayableInput } from "../api";
import { payableSchema, wholeNumber, type PayableValues } from "../schemas";

/** Whole-request refusals that belong to one box. */
const codeFields = {
  "Payables.CategoryNotFound": "categoryId",
} as const;

interface PayableFormProps {
  defaultValues: PayableValues;
  categories: ExpenseCategory[];
  submitLabel: string;
  /** Sends the form; a rejection is a server problem and lands on the form's fields. */
  onSubmit: (input: PayableInput) => Promise<void>;
  onCancel: () => void;
}

/**
 * A cheque the gym gave or one instalment, for registering one and for correcting a pending one
 * (BUSINESS_RULES.md §9 *Cheques and instalments*). The amount goes through `MoneyField` and the
 * date through the Jalali picker, like every amount and date in the app. The expense category is
 * where its expense is recorded once it is paid; an instalment also says «قسط n از N».
 */
export function PayableForm({
  defaultValues,
  categories,
  submitLabel,
  onSubmit,
  onCancel,
}: PayableFormProps) {
  const form = useForm({ resolver: zodResolver(payableSchema), defaultValues });
  const kind = useWatch({ control: form.control, name: "kind" });
  const installment = kind === "Installment";

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit({
        kind: values.kind,
        amount: normalizeMoney(values.amount),
        dueDate: values.dueDate,
        payee: values.payee,
        description: values.description,
        categoryId: values.categoryId,
        // A cheque has no instalment numbers: the API refuses them rather than ignoring them.
        installmentNumber: installment ? wholeNumber(values.installmentNumber) : null,
        installmentCount: installment ? wholeNumber(values.installmentCount) : null,
      });
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="space-y-3" onSubmit={submit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <div className="grid gap-3 sm:grid-cols-3">
        <SelectField label="نوع" error={errors.kind?.message} {...form.register("kind")}>
          {payableKinds.map((option) => (
            <option key={option} value={option}>
              {payableKindLabels[option]}
            </option>
          ))}
        </SelectField>
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
              label={installment ? "تاریخ سررسید قسط" : "تاریخ چک"}
              error={errors.dueDate?.message}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
            />
          )}
        />
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <FormField
          label={installment ? "پرداخت به" : "در وجه"}
          placeholder={installment ? "مثلاً بانک یا فروشنده" : undefined}
          autoComplete="off"
          error={errors.payee?.message}
          {...form.register("payee")}
        />
        <SelectField
          label="دسته‌بندی هزینه"
          error={errors.categoryId?.message}
          {...form.register("categoryId")}
        >
          <option value="">انتخاب کنید</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </SelectField>
        {installment && (
          <div className="grid grid-cols-2 gap-2">
            <FormField
              label="شمارهٔ قسط"
              inputMode="numeric"
              autoComplete="off"
              error={errors.installmentNumber?.message}
              {...form.register("installmentNumber")}
            />
            <FormField
              label="از چند قسط"
              inputMode="numeric"
              autoComplete="off"
              error={errors.installmentCount?.message}
              {...form.register("installmentCount")}
            />
          </div>
        )}
      </div>

      <TextareaField
        label="شرح"
        rows={2}
        placeholder="مثلاً تردمیل"
        error={errors.description?.message}
        {...form.register("description")}
      />

      <p className="text-xs text-muted-foreground">
        بعد از پاس شدن یا پرداخت، مبلغ در همین دسته‌بندی به هزینه‌ها اضافه می‌شود.
      </p>

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
