import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useVoidServiceCharge } from "../api";
import { emptyVoidServiceChargeValues, voidServiceChargeSchema } from "../schemas";

const codeFields = {
  "ServiceCharges.VoidReasonRequired": "reason",
  "ServiceCharges.VoidReasonTooLong": "reason",
} as const;

interface VoidServiceChargeFormProps {
  serviceChargeId: string;
  /** True when money has been taken against this charge, so the void will give it back. */
  refundWarning: boolean;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Undoes a charge without erasing it (BUSINESS_RULES.md §7, §5: financial records are never
 * deleted). The reason is the whole record of what happened, so it is required; the API refuses a
 * blank one too.
 */
export function VoidServiceChargeForm({
  serviceChargeId,
  refundWarning,
  onDone,
  onCancel,
}: VoidServiceChargeFormProps) {
  const voidCharge = useVoidServiceCharge();

  const form = useForm({
    resolver: zodResolver(voidServiceChargeSchema),
    defaultValues: emptyVoidServiceChargeValues,
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await voidCharge.mutateAsync({ id: serviceChargeId, reason: values.reason });
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

      {refundWarning && (
        <Alert>مبلغی که برای این مورد پرداخت شده است، با همین دلیل به عضو بازگردانده می‌شود.</Alert>
      )}

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
