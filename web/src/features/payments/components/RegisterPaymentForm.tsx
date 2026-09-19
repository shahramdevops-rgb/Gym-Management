import { useForm } from "react-hook-form";

import { FormField, MoneyField, SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { paymentMethodLabels, paymentMethods, useRegisterPayment } from "../api";
import { emptyRegisterPaymentValues, normalizeAmount, registerPaymentSchema } from "../schemas";

const codeFields = {
  "Payments.AmountNotPositive": "amount",
  "Payments.AmountTooLarge": "amount",
  "Payments.AmountTooManyDecimals": "amount",
  "Payments.MethodInvalid": "method",
  "Payments.ReferenceNumberTooLong": "referenceNumber",
  "Payments.Overpayment": "amount",
} as const;

interface RegisterPaymentFormProps {
  subscriptionId: string;
  onDone: () => void;
  onCancel: () => void;
}

/** Opens under a subscription's row in the history table: partial or full payment (BUSINESS_RULES.md §5). */
export function RegisterPaymentForm({ subscriptionId, onDone, onCancel }: RegisterPaymentFormProps) {
  const registerPayment = useRegisterPayment();

  const form = useForm({
    resolver: zodResolver(registerPaymentSchema),
    defaultValues: emptyRegisterPaymentValues,
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await registerPayment.mutateAsync({
        subscriptionId,
        amount: normalizeAmount(values.amount),
        method: values.method,
        referenceNumber: values.referenceNumber.trim() === "" ? null : values.referenceNumber.trim(),
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
          label="مبلغ (تومان)"
          placeholder="۹۰۰٬۰۰۰"
          error={errors.amount?.message}
          {...form.register("amount")}
        />
      </div>
      <div className="w-36">
        <SelectField label="روش پرداخت" error={errors.method?.message} {...form.register("method")}>
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
      <Button type="submit" size="sm" disabled={isSubmitting}>
        تأیید پرداخت
      </Button>
      <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
        انصراف
      </Button>
    </form>
  );
}
