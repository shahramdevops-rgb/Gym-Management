import type { ReactNode } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useCancelPayable, useRevertPayable } from "../api";
import { cancelPayableSchema, revertPayableSchema } from "../schemas";

interface ReasonFormProps {
  schema: z.ZodType<{ reason: string }, { reason: string }>;
  warning: ReactNode;
  label: string;
  confirmLabel: string;
  send: (reason: string) => Promise<unknown>;
  onDone: () => void;
  onCancel: () => void;
}

/** A required reason, a warning about what it does, and a confirm button. */
function ReasonForm({
  schema,
  warning,
  label,
  confirmLabel,
  send,
  onDone,
  onCancel,
}: ReasonFormProps) {
  const form = useForm({ resolver: zodResolver(schema), defaultValues: { reason: "" } });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await send(values.reason);
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

      <Alert>{warning}</Alert>

      <FormField label={label} error={errors.reason?.message} {...form.register("reason")} />

      <div className="flex gap-2">
        <Button type="submit" size="sm" variant="destructive" disabled={isSubmitting}>
          {confirmLabel}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </form>
  );
}

interface PayableReasonFormProps {
  payableId: string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Takes a pending one out of the reminder without erasing it (BUSINESS_RULES.md §9 *Cheques and
 * instalments*: cancelled with a reason, never deleted). Cancelling is final, so the form says so.
 */
export function CancelPayableForm({ payableId, onDone, onCancel }: PayableReasonFormProps) {
  const cancelPayable = useCancelPayable();

  return (
    <ReasonForm
      schema={cancelPayableSchema}
      warning="مورد باطل‌شده در فهرست می‌ماند، دیگر یادآوری نمی‌شود و قابل ویرایش نیست. برای اصلاح، مورد تازه‌ای ثبت کنید."
      label="دلیل ابطال"
      confirmLabel="تأیید ابطال"
      send={(reason) => cancelPayable.mutateAsync({ id: payableId, reason })}
      onDone={onDone}
      onCancel={onCancel}
    />
  );
}

/**
 * «برگشت به در انتظار»: a payment marked by mistake goes back, and its expense is voided with the
 * same reason (BUSINESS_RULES.md §9 *Cheques and instalments*).
 */
export function RevertPayableForm({ payableId, onDone, onCancel }: PayableReasonFormProps) {
  const revertPayable = useRevertPayable();

  return (
    <ReasonForm
      schema={revertPayableSchema}
      warning="به در انتظار برمی‌گردد و هزینه‌ای که با پرداختش ثبت شده بود، با همین دلیل باطل می‌شود."
      label="دلیل برگشت"
      confirmLabel="تأیید برگشت"
      send={(reason) => revertPayable.mutateAsync({ id: payableId, reason })}
      onDone={onDone}
      onCancel={onCancel}
    />
  );
}
