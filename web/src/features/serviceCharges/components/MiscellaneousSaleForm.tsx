import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";

import { FormField, MoneyField, SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { paymentMethodLabels, paymentMethods, type PaymentMethod } from "@/features/payments/api";
import { ConfirmPaymentDialog } from "@/features/payments/components/ConfirmPaymentDialog";
import { formatMoney } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { multiplyMoney, normalizeMoney } from "@/lib/money";

import { useRecordMiscellaneousSale, type ServiceCharge } from "../api";
import {
  emptyMiscellaneousSaleValues,
  miscellaneousSaleLimits,
  miscellaneousSaleSchema,
  onAccount,
  parseSaleQuantity,
  serviceChargeAmountProblem,
  type MiscellaneousSaleValues,
} from "../schemas";

const codeFields = {
  "ServiceCharges.DescriptionRequired": "description",
  "ServiceCharges.DescriptionTooLong": "description",
  "ServiceCharges.QuantityInvalid": "quantity",
  "ServiceCharges.AmountNotPositive": "unitPrice",
  "ServiceCharges.AmountTooLarge": "unitPrice",
  "ServiceCharges.AmountTooManyDecimals": "unitPrice",
  "Payments.MethodInvalid": "payment",
} as const;

/** What is about to be sent: the checked values with the method already split from «به حساب». */
interface SaleToSend {
  description: string;
  quantity: number;
  unitPrice: string;
  method: PaymentMethod | null;
  total: string;
}

interface MiscellaneousSaleFormProps {
  attendanceId: string;
  onDone: (sale: ServiceCharge) => void;
  onCancel: () => void;
}

/**
 * «فروش متفرقه» (BUSINESS_RULES.md §7 *Miscellaneous sale*): something the desk sells that has no
 * product of its own, so the desk types its name, how many, the price of one and how it was paid.
 *
 * Paid now, the desk is asked "was the money received?" before anything is sent, like every other
 * money form (§5 *Confirming money at the desk*). Left on the member's account, no money moves and
 * nothing is asked, like a cafe purchase from the locker.
 */
export function MiscellaneousSaleForm({ attendanceId, onDone, onCancel }: MiscellaneousSaleFormProps) {
  const recordSale = useRecordMiscellaneousSale();
  const [toConfirm, setToConfirm] = useState<SaleToSend | null>(null);

  const form = useForm<MiscellaneousSaleValues>({
    resolver: zodResolver(miscellaneousSaleSchema),
    defaultValues: emptyMiscellaneousSaleValues,
  });

  const quantity = parseSaleQuantity(useWatch({ control: form.control, name: "quantity" }) ?? "");
  const unitPrice = useWatch({ control: form.control, name: "unitPrice" }) ?? "";
  const total =
    quantity !== null && serviceChargeAmountProblem(unitPrice) === null
      ? multiplyMoney(normalizeMoney(unitPrice), quantity)
      : "";

  const onSubmit = form.handleSubmit((values) => {
    const sale: SaleToSend = {
      description: values.description,
      // The schema has already refused anything that is not a whole number from 1 to 999.
      quantity: parseSaleQuantity(values.quantity)!,
      unitPrice: normalizeMoney(values.unitPrice),
      method: values.payment === onAccount ? null : values.payment,
      total,
    };

    if (sale.method === null) {
      void send(sale);
    } else {
      setToConfirm(sale);
    }
  });

  async function send(sale: SaleToSend) {
    try {
      const saved = await recordSale.mutateAsync({
        attendanceId,
        description: sale.description,
        quantity: sale.quantity,
        unitPrice: sale.unitPrice,
        method: sale.method,
      });
      onDone(saved);
    } catch (problem) {
      setToConfirm(null);
      applyServerErrors(problem, form.setError, codeFields);
    }
  }

  const { errors } = form.formState;

  return (
    <>
      <form className="space-y-3" onSubmit={onSubmit} noValidate>
        {errors.root?.server !== undefined && (
          <Alert variant="destructive">{errors.root.server.message}</Alert>
        )}

        <FormField
          label="نام کالا"
          autoComplete="off"
          maxLength={miscellaneousSaleLimits.descriptionMaxLength}
          error={errors.description?.message}
          {...form.register("description")}
        />

        <div className="grid gap-3 sm:grid-cols-[8rem_1fr]">
          <FormField
            label="تعداد"
            dir="ltr"
            inputMode="numeric"
            autoComplete="off"
            error={errors.quantity?.message}
            {...form.register("quantity")}
          />
          <Controller
            control={form.control}
            name="unitPrice"
            render={({ field }) => (
              <MoneyField
                label="قیمت واحد (تومان)"
                placeholder="۱۰۰٬۰۰۰"
                error={errors.unitPrice?.message}
                name={field.name}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
              />
            )}
          />
        </div>

        <div className="flex items-center justify-between rounded-md border px-3 py-2 font-bold">
          <span>جمع</span>
          <span>{total === "" ? "—" : formatMoney(total)}</span>
        </div>

        <SelectField label="نوع پرداخت" error={errors.payment?.message} {...form.register("payment")}>
          {/* Not `disabled`, for the reason PaymentMethodField gives: the desk must choose. */}
          <option value="">انتخاب کنید…</option>
          {paymentMethods.map((method) => (
            <option key={method} value={method}>
              {paymentMethodLabels[method]}
            </option>
          ))}
          <option value={onAccount}>به حساب عضو</option>
        </SelectField>

        <div className="flex gap-2">
          <Button type="submit" disabled={recordSale.isPending}>
            {recordSale.isPending ? "در حال ثبت…" : "ثبت فروش"}
          </Button>
          <Button type="button" variant="ghost" onClick={onCancel}>
            انصراف
          </Button>
        </div>
      </form>

      <ConfirmPaymentDialog
        payment={
          toConfirm === null || toConfirm.method === null
            ? null
            : { amount: toConfirm.total, method: toConfirm.method }
        }
        pending={recordSale.isPending}
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
