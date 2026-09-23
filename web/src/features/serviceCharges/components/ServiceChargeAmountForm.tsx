import { Controller, useForm } from "react-hook-form";

import { MoneyField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

import {
  useChangeServiceChargeAmount,
  useRecordServiceCharge,
  type ServiceChargeKind,
} from "../api";
import { emptyServiceChargeAmountValues, serviceChargeAmountSchema } from "../schemas";

const codeFields = {
  "ServiceCharges.AmountNotPositive": "amount",
  "ServiceCharges.AmountTooLarge": "amount",
  "ServiceCharges.AmountTooManyDecimals": "amount",
} as const;

/**
 * Either the visit a new charge is recorded against, or the charge whose amount is being
 * corrected. One shape or the other, never both: recording and correcting are different
 * endpoints, and this is the one place that has to know which it is doing.
 */
export type ServiceChargeAmountTarget =
  | { attendanceId: string; kind: ServiceChargeKind }
  | { id: string };

interface ServiceChargeAmountFormProps {
  label: string;
  target: ServiceChargeAmountTarget;
  initialAmount?: string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Types the amount for a gym service (BUSINESS_RULES.md §7). The gym's rate is not in the system
 * on purpose — staff work the figure out at the desk and this takes what they type — so the guard
 * against a miscounted zero is the shared `MoneyField` and the amount in words underneath it.
 */
export function ServiceChargeAmountForm({
  label,
  target,
  initialAmount,
  onDone,
  onCancel,
}: ServiceChargeAmountFormProps) {
  const record = useRecordServiceCharge();
  const changeAmount = useChangeServiceChargeAmount();

  const form = useForm({
    resolver: zodResolver(serviceChargeAmountSchema),
    defaultValues:
      initialAmount === undefined
        ? emptyServiceChargeAmountValues
        : { amount: initialAmount },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    const amount = normalizeMoney(values.amount);
    try {
      if ("id" in target) {
        await changeAmount.mutateAsync({ id: target.id, amount });
      } else {
        await record.mutateAsync({ ...target, amount });
      }
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
              label={label}
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
      <Button type="submit" size="sm" disabled={isSubmitting}>
        ثبت
      </Button>
      <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
        انصراف
      </Button>
    </form>
  );
}
