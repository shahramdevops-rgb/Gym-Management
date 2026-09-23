import { Controller, useForm } from "react-hook-form";

import { FormField, MoneyField, SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { paymentMethodLabels, paymentMethods } from "@/features/payments/api";
import { emptyRegisterPaymentValues, registerPaymentSchema } from "@/features/payments/schemas";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

import { useRegisterServiceChargePayment } from "../api";

const codeFields = {
  "Payments.AmountNotPositive": "amount",
  "Payments.AmountTooLarge": "amount",
  "Payments.AmountTooManyDecimals": "amount",
  "Payments.MethodInvalid": "method",
  "Payments.ReferenceNumberTooLong": "referenceNumber",
  "Payments.Overpayment": "amount",
} as const;

interface ServiceChargePaymentFormProps {
  serviceChargeId: string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Money against a هوازی charge (BUSINESS_RULES.md §7: "a service charge is paid like anything
 * else"). The same schema, fields and error codes as a subscription payment — only the endpoint
 * differs, which is what "like anything else" is supposed to mean.
 */
export function ServiceChargePaymentForm({
  serviceChargeId,
  onDone,
  onCancel,
}: ServiceChargePaymentFormProps) {
  const registerPayment = useRegisterServiceChargePayment();

  const form = useForm({
    resolver: zodResolver(registerPaymentSchema),
    defaultValues: emptyRegisterPaymentValues,
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await registerPayment.mutateAsync({
        id: serviceChargeId,
        amount: normalizeMoney(values.amount),
        method: values.method,
        referenceNumber:
          values.referenceNumber.trim() === "" ? null : values.referenceNumber.trim(),
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
        <Controller
          control={form.control}
          name="amount"
          render={({ field }) => (
            <MoneyField
              label="مبلغ (تومان)"
              placeholder="۱۰٬۰۰۰"
              error={errors.amount?.message}
              name={field.name}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
            />
          )}
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
