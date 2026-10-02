import { useState } from "react";
import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { ConfirmPaymentDialog } from "@/features/payments/components/ConfirmPaymentDialog";
import { PaymentMethodField } from "@/features/payments/components/PaymentMethodField";
import { registerPaymentSchema, type RegisterPaymentValues } from "@/features/payments/schemas";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { formatMoney } from "@/lib/format";
import { normalizeMoney } from "@/lib/money";

import { useSettleGuestCafe } from "../api";

const codeFields = {
  "Payments.MethodInvalid": "method",
  "Payments.ReferenceNumberTooLong": "referenceNumber",
} as const;

interface GuestSettleFormProps {
  attendanceId: string;
  /** Everything the visit's cafe orders still owe, as the box shows it. It is what is paid. */
  outstanding: number | string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * «تسویه یکجا» for a guest (BUSINESS_RULES.md §7 *Guest visit*): every unpaid cafe order of the
 * visit, in one step, so the guest can check out. There is nothing to choose and no amount to
 * type: a guest leaves no debt behind, so the whole of it is paid. Only the method (and a
 * reference number for a card) is asked, then the same "was the money received?" box as every
 * other payment (§5 *Confirming money at the desk*).
 */
export function GuestSettleForm({
  attendanceId,
  outstanding,
  onDone,
  onCancel,
}: GuestSettleFormProps) {
  const settle = useSettleGuestCafe();
  const [toConfirm, setToConfirm] = useState<RegisterPaymentValues | null>(null);
  const amount = normalizeMoney(String(outstanding));

  const form = useForm<RegisterPaymentValues>({
    resolver: zodResolver(registerPaymentSchema),
    defaultValues: { amount, referenceNumber: "" },
  });

  const onSubmit = form.handleSubmit((values) => setToConfirm(values));

  async function send(values: RegisterPaymentValues) {
    try {
      await settle.mutateAsync({
        attendanceId,
        amount,
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
      <form className="space-y-3" onSubmit={onSubmit} noValidate aria-label="تسویه یکجا">
        {errors.root?.server !== undefined && (
          <Alert variant="destructive">{errors.root.server.message}</Alert>
        )}
        <p className="text-sm">
          مبلغ کل: <strong>{formatMoney(outstanding)}</strong>
        </p>
        <div className="flex flex-wrap items-end gap-3">
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
        </div>
        <div className="flex gap-2">
          <Button type="submit" size="sm" disabled={settle.isPending}>
            تأیید پرداخت
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
            انصراف
          </Button>
        </div>
      </form>

      <ConfirmPaymentDialog
        payment={toConfirm === null ? null : { amount, method: toConfirm.method }}
        pending={settle.isPending}
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
