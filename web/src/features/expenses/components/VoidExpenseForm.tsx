import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useVoidExpense } from "../api";
import { voidExpenseSchema } from "../schemas";

const codeFields = {
  "Expenses.VoidReasonRequired": "reason",
  "Expenses.VoidReasonTooLong": "reason",
} as const;

interface VoidExpenseFormProps {
  expenseId: string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Takes an expense out of the books without erasing it (BUSINESS_RULES.md §9: voided with a
 * reason, never deleted). A voided expense is final — it cannot be edited or voided again — so
 * the form says so before the button is pressed.
 */
export function VoidExpenseForm({ expenseId, onDone, onCancel }: VoidExpenseFormProps) {
  const voidExpense = useVoidExpense();

  const form = useForm({
    resolver: zodResolver(voidExpenseSchema),
    defaultValues: { reason: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await voidExpense.mutateAsync({ id: expenseId, reason: values.reason });
      onDone();
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="space-y-3" onSubmit={onSubmit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <Alert>
        هزینهٔ باطل‌شده از جمع هزینه‌ها و گزارش‌ها کنار گذاشته می‌شود و دیگر قابل ویرایش نیست. برای
        اصلاح، هزینهٔ تازه‌ای ثبت کنید.
      </Alert>

      <FormField label="دلیل ابطال" error={errors.reason?.message} {...form.register("reason")} />

      <div className="flex gap-2">
        <Button type="submit" size="sm" variant="destructive" disabled={isSubmitting}>
          تأیید ابطال
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </form>
  );
}
