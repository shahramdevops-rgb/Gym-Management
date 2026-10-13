import { useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";

import { TextareaField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { paymentKeys } from "@/features/payments/api";
import { formatMoney } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { isPositiveMoney } from "@/lib/money";
import { normalizePersianText } from "@/lib/normalize";

import { useCancelSubscription } from "../api";
import { cancelSubscriptionSchema } from "../schemas";

const codeFields = {
  "Subscriptions.CancelReasonRequired": "reason",
  "Subscriptions.CancelReasonTooLong": "reason",
} as const;

interface CancelSubscriptionFormProps {
  subscriptionId: string;
  /** What has been paid on it, all of which the cancel gives back. */
  netPaid: number | string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Opens under a subscription's row in the history table (BUSINESS_RULES.md §4 Cancel). Owner only.
 * Cancelling gives back everything paid on it, the way it was paid (roadmap 6.5.37), so the box
 * says how much before the Owner confirms, as the cafe's cancel does.
 */
export function CancelSubscriptionForm({
  subscriptionId,
  netPaid,
  onDone,
  onCancel,
}: CancelSubscriptionFormProps) {
  const cancelSubscription = useCancelSubscription();
  const queryClient = useQueryClient();

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
      // The refunds the cancel wrote belong in «پرداخت‌ها» too. Refreshed here, not in the hook,
      // because payments/api already imports subscriptions/api.
      await queryClient.invalidateQueries({ queryKey: paymentKeys.all });
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

      {isPositiveMoney(netPaid) && (
        <Alert className="basis-full">
          {formatMoney(netPaid)} پرداخت‌شده برای این اشتراک، به همان روشی که پرداخت شده بود
          بازگردانده می‌شود.
        </Alert>
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
