import { useForm } from "react-hook-form";

import { FormField, MoneyField, SelectField, TextareaField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizePersianText } from "@/lib/normalize";

import { paymentMethodLabels, paymentMethods, useRegisterRefund } from "../api";
import { emptyRegisterRefundValues, normalizeAmount, registerRefundSchema } from "../schemas";

const codeFields = {
  "Payments.AmountNotPositive": "amount",
  "Payments.AmountTooLarge": "amount",
  "Payments.AmountTooManyDecimals": "amount",
  "Payments.MethodInvalid": "method",
  "Payments.ReferenceNumberTooLong": "referenceNumber",
  "Payments.RefundExceedsNetPaid": "amount",
  "Payments.RefundReasonRequired": "reason",
  "Payments.RefundReasonTooLong": "reason",
} as const;

interface RegisterRefundFormProps {
  subscriptionId: string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Opens under a subscription's row in the history table: a refund, or a "void" when it is the
 * full amount of a mistaken payment (BUSINESS_RULES.md §5). Owner only.
 */
export function RegisterRefundForm({ subscriptionId, onDone, onCancel }: RegisterRefundFormProps) {
  const registerRefund = useRegisterRefund();

  const form = useForm({
    resolver: zodResolver(registerRefundSchema),
    defaultValues: emptyRegisterRefundValues,
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await registerRefund.mutateAsync({
        subscriptionId,
        amount: normalizeAmount(values.amount),
        method: values.method,
        referenceNumber: values.referenceNumber.trim() === "" ? null : values.referenceNumber.trim(),
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

      <div className="w-40">
        <MoneyField
          label="مبلغ استرداد (تومان)"
          placeholder="۹۰۰٬۰۰۰"
          error={errors.amount?.message}
          {...form.register("amount")}
        />
      </div>
      <div className="w-36">
        <SelectField label="روش" error={errors.method?.message} {...form.register("method")}>
          {paymentMethods.map((method) => (
            <option key={method} value={method}>
              {paymentMethodLabels[method]}
            </option>
          ))}
        </SelectField>
      </div>
      <div className="min-w-40 flex-1">
        <FormField
          label="شماره پیگیری (اختیاری)"
          dir="ltr"
          autoComplete="off"
          error={errors.referenceNumber?.message}
          {...form.register("referenceNumber")}
        />
      </div>
      <div className="min-w-64 flex-1 basis-full">
        <TextareaField label="دلیل استرداد" error={errors.reason?.message} {...form.register("reason")} />
      </div>
      <Button type="submit" size="sm" variant="destructive" disabled={isSubmitting}>
        تأیید استرداد
      </Button>
      <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
        انصراف
      </Button>
    </form>
  );
}
