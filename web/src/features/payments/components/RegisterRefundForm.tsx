import { useState } from "react";
import { Controller, useForm } from "react-hook-form";

import { FormField, MoneyField, TextareaField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";
import { normalizePersianText } from "@/lib/normalize";

import { useRegisterRefund } from "../api";
import {
  emptyRegisterRefundValues,
  registerRefundSchema,
  type RegisterRefundValues,
} from "../schemas";
import { ConfirmPaymentDialog } from "./ConfirmPaymentDialog";
import { PaymentMethodField } from "./PaymentMethodField";

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
  // The checked values waiting for "was the money handed back?"; nothing is sent before the answer.
  const [toConfirm, setToConfirm] = useState<RegisterRefundValues | null>(null);

  const form = useForm<RegisterRefundValues>({
    resolver: zodResolver(registerRefundSchema),
    defaultValues: emptyRegisterRefundValues,
  });

  const onSubmit = form.handleSubmit((values) => setToConfirm(values));

  async function send(values: RegisterRefundValues) {
    try {
      await registerRefund.mutateAsync({
        subscriptionId,
        amount: normalizeMoney(values.amount),
        method: values.method,
        referenceNumber:
          values.referenceNumber.trim() === "" ? null : values.referenceNumber.trim(),
        reason: normalizePersianText(values.reason),
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
                label="مبلغ استرداد (تومان)"
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
          <PaymentMethodField
            label="روش"
            error={errors.method?.message}
            {...form.register("method")}
          />
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
          <TextareaField
            label="دلیل استرداد"
            error={errors.reason?.message}
            {...form.register("reason")}
          />
        </div>
        <Button type="submit" size="sm" variant="destructive" disabled={registerRefund.isPending}>
          تأیید استرداد
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
        direction="out"
        pending={registerRefund.isPending}
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
