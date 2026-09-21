import { useForm } from "react-hook-form";

import { TextareaField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizePersianText } from "@/lib/normalize";

import { useCancelSubscription } from "../api";
import { cancelSubscriptionSchema } from "../schemas";

const codeFields = {
  "Subscriptions.CancelReasonRequired": "reason",
  "Subscriptions.CancelReasonTooLong": "reason",
} as const;

interface CancelSubscriptionFormProps {
  subscriptionId: string;
  onDone: () => void;
  onCancel: () => void;
}

/** Opens under a subscription's row in the history table: cancel, whatever its status (BUSINESS_RULES.md §4 Cancel). Owner only. */
export function CancelSubscriptionForm({
  subscriptionId,
  onDone,
  onCancel,
}: CancelSubscriptionFormProps) {
  const cancelSubscription = useCancelSubscription();

  const form = useForm({
    resolver: zodResolver(cancelSubscriptionSchema),
    defaultValues: { reason: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await cancelSubscription.mutateAsync({
        id: subscriptionId,
        reason: normalizePersianText(values.reason),
      });
      onDone();
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="flex flex-wrap items-end gap-3" onSubmit={onSubmit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <div className="min-w-64 flex-1">
        <TextareaField
          label="دلیل لغو"
          error={errors.reason?.message}
          {...form.register("reason")}
        />
      </div>
      <Button type="submit" size="sm" variant="destructive" disabled={isSubmitting}>
        تأیید لغو
      </Button>
      <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
        انصراف
      </Button>
    </form>
  );
}
