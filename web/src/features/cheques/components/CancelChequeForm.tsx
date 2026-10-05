import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useCancelCheque } from "../api";
import { cancelChequeSchema } from "../schemas";

interface CancelChequeFormProps {
  chequeId: string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Takes a pending cheque out of the reminder without erasing it (BUSINESS_RULES.md §9 *Cheques*:
 * cancelled with a reason, never deleted). Cancelling is final, so the form says so first.
 */
export function CancelChequeForm({ chequeId, onDone, onCancel }: CancelChequeFormProps) {
  const cancelCheque = useCancelCheque();

  const form = useForm({
    resolver: zodResolver(cancelChequeSchema),
    defaultValues: { reason: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await cancelCheque.mutateAsync({ id: chequeId, reason: values.reason });
      onDone();
    } catch (problem) {
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="space-y-3" onSubmit={onSubmit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <Alert>
        چک باطل‌شده در فهرست می‌ماند، دیگر یادآوری نمی‌شود و قابل ویرایش نیست. برای اصلاح، چک
        تازه‌ای ثبت کنید.
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
