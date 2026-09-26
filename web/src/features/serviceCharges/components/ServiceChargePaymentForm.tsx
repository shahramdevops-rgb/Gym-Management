import { useState } from "react";
import { Controller, useForm } from "react-hook-form";

import { FormField, MoneyField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { ConfirmPaymentDialog } from "@/features/payments/components/ConfirmPaymentDialog";
import { PaymentMethodField } from "@/features/payments/components/PaymentMethodField";
import {
  emptyRegisterPaymentValues,
  registerPaymentSchema,
  type RegisterPaymentValues,
} from "@/features/payments/schemas";
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
  // The checked values waiting for "was the money received?"; nothing is sent before the answer.
  const [toConfirm, setToConfirm] = useState<RegisterPaymentValues | null>(null);

  const form = useForm<RegisterPaymentValues>({
    resolver: zodResolver(registerPaymentSchema),
    defaultValues: emptyRegisterPaymentValues,
  });

  const onSubmit = form.handleSubmit((values) => setToConfirm(values));

  async function send(values: RegisterPaymentValues) {
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
      setToConfirm(null);
      applyServerErrors(problem, form.setError, codeFields);
    }
  }

  const { errors } = form.formState;

  return (
    <>
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
          <PaymentMethodField error={errors.method?.message} {...form.register("method")} />
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
        <Button type="submit" size="sm" disabled={registerPayment.isPending}>
          تأیید پرداخت
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </form>

      <ConfirmPaymentDialog
        payment={
          toConfirm === null
            ? null
            : { amount: normalizeMoney(toConfirm.amount), method: toConfirm.method }
        }
        pending={registerPayment.isPending}
        onConfirm={() => {
          if (toConfirm !== null) {
            void send(toConfirm);
          }
        }}
        onCancel={() => setToConfirm(null)}
      />
    </>
  );
}
