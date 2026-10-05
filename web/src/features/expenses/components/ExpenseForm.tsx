import { Controller, useForm } from "react-hook-form";

import {
  FormField,
  JalaliDateField,
  MoneyField,
  SelectField,
  TextareaField,
} from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

import type { ExpenseCategory, ExpenseInput } from "../api";
import { expenseSchema, type ExpenseValues } from "../schemas";

/** Whole-request refusals that belong to one box. */
const codeFields = {
  "Expenses.CategoryNotFound": "categoryId",
  "Expenses.DateInFuture": "expenseDate",
} as const;

interface ExpenseFormProps {
  defaultValues: ExpenseValues;
  categories: ExpenseCategory[];
  submitLabel: string;
  /** Sends the form; a rejection is a server problem and lands on the form's fields. */
  onSubmit: (input: ExpenseInput) => Promise<void>;
  onCancel: () => void;
}

/**
 * What the gym paid, for recording an expense and for correcting one (BUSINESS_RULES.md §9). The
 * amount goes through `MoneyField` and the date through the Jalali picker, like every amount and
 * date in the app; the date may be any past day, so an old bill can still be entered, but not a
 * day that has not happened yet.
 */
export function ExpenseForm({
  defaultValues,
  categories,
  submitLabel,
  onSubmit,
  onCancel,
}: ExpenseFormProps) {
  const form = useForm({ resolver: zodResolver(expenseSchema), defaultValues });

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit({
        amount: normalizeMoney(values.amount),
        categoryId: values.categoryId,
        expenseDate: values.expenseDate,
        description: values.description,
        // An empty box means "no reference", which the API spells null.
        referenceNumber: values.referenceNumber === "" ? null : values.referenceNumber,
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
        <Controller
          control={form.control}
          name="amount"
          render={({ field }) => (
            <MoneyField
              label="مبلغ (تومان)"
              placeholder="۵۰۰٬۰۰۰"
              error={errors.amount?.message}
              name={field.name}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
            />
          )}
        />
        <SelectField
          label="دسته‌بندی"
          error={errors.categoryId?.message}
          {...form.register("categoryId")}
        >
          <option value="">انتخاب کنید…</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </SelectField>
        <Controller
          control={form.control}
          name="expenseDate"
          render={({ field }) => (
            <JalaliDateField
              label="تاریخ هزینه"
              error={errors.expenseDate?.message}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
            />
          )}
        />
      </div>

      <TextareaField
        label="شرح"
        rows={2}
        error={errors.description?.message}
        {...form.register("description")}
      />

      <div className="max-w-xs">
        <FormField
          label="شمارهٔ مرجع (اختیاری)"
          autoComplete="off"
          error={errors.referenceNumber?.message}
          {...form.register("referenceNumber")}
        />
      </div>

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
