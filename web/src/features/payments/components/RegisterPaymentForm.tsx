import { useState } from "react";
import { Controller, useForm } from "react-hook-form";

import { FormField, MoneyField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

import { useRegisterPayment } from "../api";
import {
  emptyRegisterPaymentValues,
  registerPaymentSchema,
  type RegisterPaymentValues,
} from "../schemas";
import { ConfirmPaymentDialog } from "./ConfirmPaymentDialog";
import { PaymentMethodField } from "./PaymentMethodField";

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
export function RegisterPaymentForm({
  subscriptionId,
  onDone,
  onCancel,
}: RegisterPaymentFormProps) {
  const registerPayment = useRegisterPayment();
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
        subscriptionId,
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
                placeholder="۹۰۰٬۰۰۰"
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
