import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { formatMoney } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useCancelCafeOrder } from "../api";
import { cancelOrderSchema } from "../schemas";

const codeFields = {
  "CafeOrders.CancelReasonRequired": "reason",
  "CafeOrders.CancelReasonTooLong": "reason",
} as const;

interface CancelCafeOrderFormProps {
  orderId: string;
  /** What was paid on the order, net of refunds; zero when nothing was. */
  netPaid: number | string;
  /** True when money was taken, so the cancellation will give it back. */
  refundWarning: boolean;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Takes a sale back without erasing it (BUSINESS_RULES.md §8: an order is never edited, it is
 * cancelled with a reason and rung up again). Whatever was paid goes back the way it came, one
 * refund per method, so the form never asks how to return the money — it only says that it will.
 */
export function CancelCafeOrderForm({
  orderId,
  netPaid,
  refundWarning,
  onDone,
  onCancel,
}: CancelCafeOrderFormProps) {
  const cancelOrder = useCancelCafeOrder();

  const form = useForm({
    resolver: zodResolver(cancelOrderSchema),
    defaultValues: { reason: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await cancelOrder.mutateAsync({ id: orderId, reason: values.reason });
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
        <Alert>
          {formatMoney(netPaid)} پرداخت‌شده برای این سفارش، به همان روشی که پرداخت شده بود
          بازگردانده می‌شود.
        </Alert>
      )}

      <FormField label="دلیل لغو" error={errors.reason?.message} {...form.register("reason")} />

      <div className="flex gap-2">
        <Button type="submit" size="sm" variant="destructive" disabled={isSubmitting}>
          تأیید لغو
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </form>
  );
}
