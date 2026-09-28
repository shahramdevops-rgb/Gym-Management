import { CheckCircle2 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";

import { FormField, MoneyField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { MemberDebtItem } from "@/features/members/api";
import { debtItemLabel } from "@/features/members/debtItemLabel";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { formatDate, formatMoney } from "@/lib/format";
import { addMoney, isPositiveMoney, normalizeMoney, subtractMoney } from "@/lib/money";

import { paymentMethodLabels, type PaymentMethod } from "../api";
import {
  emptyRegisterPaymentValues,
  registerPaymentSchema,
  type RegisterPaymentValues,
} from "../schemas";
import { useSettleDebt, type Settlement } from "../settle";
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

/** What the success step lists. Kept apart from the debt, which is refetched and emptied behind it. */
interface Receipt {
  amount: string;
  method: PaymentMethod;
  lines: { id: string; label: string; amount: string }[];
  remainingDebt: string;
}

/**
 * «تسویه یکجا»: one amount and one method over several owed items (BUSINESS_RULES.md §5 *Settling
 * several items at once*). A walk-in's single visit, هوازی and drink are one handover of money, not
 * three payment forms.
 *
 * Rendered wherever the debt is shown to the desk — the check-in/check-out box and the member's
 * debt card — and rendered even once nothing is owed, because that is exactly when the success
 * step is on screen and must not vanish with the debt it just cleared.
 */
export function SettleDebt({ memberId, items }: { memberId: string; items: MemberDebtItem[] }) {
  const [open, setOpen] = useState(false);
  const [receipt, setReceipt] = useState<Receipt | null>(null);

  if (receipt !== null) {
    return <SettlementDone receipt={receipt} onClose={() => setReceipt(null)} />;
  }
  if (items.length === 0) {
    return null;
  }
  if (!open) {
    return (
      <Button variant="outline" onClick={() => setOpen(true)}>
        تسویه یکجا
      </Button>
    );
  }

  return (
    <SettleDebtForm
      memberId={memberId}
      items={items}
      onSettled={(done) => {
        setOpen(false);
        setReceipt(done);
      }}
      onCancel={() => setOpen(false)}
    />
  );
}

/** Cafe, then هوازی, then the subscription: the order the money is spent in, so the list reads the same way. */
const kindOrder: Record<MemberDebtItem["kind"], number> = {
  CafeOrder: 0,
  ServiceCharge: 1,
  Subscription: 2,
};

function inSettlementOrder(items: MemberDebtItem[]): MemberDebtItem[] {
  return [...items].sort(
    (left, right) =>
      kindOrder[left.kind] - kindOrder[right.kind] || left.startDate.localeCompare(right.startDate),
  );
}

/** `220000.00` → `220000`: what goes into the amount box, without two zeros nobody would type. */
function asTypedAmount(total: string): string {
  return total.replace(/\.00$/, "");
}

function SettleDebtForm({
  memberId,
  items,
  onSettled,
  onCancel,
}: {
  memberId: string;
  items: MemberDebtItem[];
  onSettled: (receipt: Receipt) => void;
  onCancel: () => void;
}) {
  const settle = useSettleDebt();
  const ordered = useMemo(() => inSettlementOrder(items), [items]);

  // The items the desk *un*ticked. Stored this way round so an item that appears after a refetch
  // (the debt changed and was loaded again) starts ticked like every other.
  const [unticked, setUnticked] = useState<ReadonlySet<string>>(new Set());
  const selected = ordered.filter((item) => !unticked.has(item.id));
  const selectedTotal = addMoney(...selected.map((item) => item.outstanding));
  // The checked values waiting for "was the money received?"; nothing is sent before the answer.
  // The ticked items are kept with them: the debt is polled, and an order that arrives while the
  // question is open must not join a payment the desk has already agreed to.
  const [toConfirm, setToConfirm] = useState<{
    values: RegisterPaymentValues;
    items: MemberDebtItem[];
  } | null>(null);

  const form = useForm<RegisterPaymentValues>({
    resolver: zodResolver(registerPaymentSchema),
    defaultValues: { ...emptyRegisterPaymentValues, amount: asTypedAmount(selectedTotal) },
  });

  // A different set of items is a different total to collect, so the box follows it. A smaller
  // amount typed by hand is kept until the ticks change.
  const { setValue } = form;
  useEffect(() => {
    setValue("amount", asTypedAmount(selectedTotal));
  }, [selectedTotal, setValue]);

  function toggle(id: string) {
    setUnticked((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  const onSubmit = form.handleSubmit((values) => {
    if (subtractMoney(selectedTotal, normalizeMoney(values.amount)).startsWith("-")) {
      form.setError("amount", {
        type: "manual",
        message: "مبلغ از جمع موارد انتخاب‌شده بیشتر است.",
      });
      return;
    }
    setToConfirm({ values, items: selected });
  });

  async function send({
    values,
    items: chosen,
  }: {
    values: RegisterPaymentValues;
    items: MemberDebtItem[];
  }) {
    try {
      const settlement = await settle.mutateAsync({
        memberId,
        amount: normalizeMoney(values.amount),
        method: values.method,
        referenceNumber:
          values.referenceNumber.trim() === "" ? null : values.referenceNumber.trim(),
        // Sent back exactly as the debt gave it: the server compares it with what is owed now.
        items: chosen.map((item) => ({
          kind: item.kind,
          id: item.id,
          outstanding: item.outstanding,
        })),
      });
      onSettled(receiptOf(settlement, items));
    } catch (problem) {
      setToConfirm(null);
      applyServerErrors(problem, form.setError, codeFields);
    }
  }

  const { errors } = form.formState;
  const isSubmitting = settle.isPending;

  return (
    <>
      <form
        aria-label="تسویه یکجا"
        className="space-y-3 rounded-lg border p-3 text-sm"
        onSubmit={onSubmit}
        noValidate
      >
        <p className="font-medium">تسویه یکجا</p>

        {errors.root?.server !== undefined && (
          <Alert variant="destructive">{errors.root.server.message}</Alert>
        )}

        <fieldset className="space-y-1">
          <legend className="sr-only">موارد تسویه</legend>
          {ordered.map((item) => (
            <label key={item.id} className="flex cursor-pointer items-center gap-2">
              <input
                type="checkbox"
                className="size-4 accent-primary"
                checked={!unticked.has(item.id)}
                onChange={() => toggle(item.id)}
              />
              <span className="flex-1">
                {debtItemLabel(item)}
                <span className="text-muted-foreground"> · {formatDate(item.startDate)}</span>
              </span>
              <span className="font-medium">{formatMoney(item.outstanding)}</span>
            </label>
          ))}
        </fieldset>

        <p className="flex justify-between gap-3 border-t pt-2 font-medium">
          <span>جمع انتخاب‌شده</span>
          <span>{formatMoney(selectedTotal)}</span>
        </p>

        {selected.length === 0 ? (
          <p className="text-muted-foreground">دست‌کم یک مورد را انتخاب کنید.</p>
        ) : (
          <>
            <div className="flex flex-wrap items-start gap-3">
              <div className="w-44">
                <Controller
                  control={form.control}
                  name="amount"
                  render={({ field }) => (
                    <MoneyField
                      label="مبلغ دریافتی (تومان)"
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
            </div>
            <p className="text-xs text-muted-foreground">
              اگر مبلغ کمتر از جمع باشد، اول بوفه، بعد هوازی و بعد اشتراک تسویه می‌شود و بقیه بدهی
              می‌ماند.
            </p>
          </>
        )}

        <div className="flex flex-wrap gap-2">
          <Button type="submit" size="sm" disabled={isSubmitting || selected.length === 0}>
            {isSubmitting ? "در حال ثبت…" : "تأیید تسویه"}
          </Button>
          <Button
            type="button"
            size="sm"
            variant="ghost"
            disabled={isSubmitting}
            onClick={onCancel}
          >
            انصراف
          </Button>
        </div>
      </form>

      <ConfirmPaymentDialog
        payment={
          toConfirm === null
            ? null
            : { amount: normalizeMoney(toConfirm.values.amount), method: toConfirm.values.method }
        }
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

/** Each payment named the way the debt named its item, before the refetch empties that list. */
function receiptOf(settlement: Settlement, items: MemberDebtItem[]): Receipt {
  const byId = new Map(items.map((item) => [item.id, item]));

  return {
    amount: String(settlement.amount),
    method: settlement.method,
    remainingDebt: String(settlement.remainingDebt),
    lines: settlement.payments.map((payment) => {
      const item = byId.get(payment.targetId);
      return {
        id: payment.paymentId,
        label: item === undefined ? "" : debtItemLabel(item),
        amount: String(payment.amount),
      };
    }),
  };
}

/** What the server wrote, line by line, and whether anything is still owed. */
function SettlementDone({ receipt, onClose }: { receipt: Receipt; onClose: () => void }) {
  return (
    <section
      role="status"
      aria-label="تسویه ثبت شد"
      className="space-y-2 rounded-lg border border-success/40 bg-success/5 p-3 text-sm"
    >
      <p className="flex items-center gap-2 font-medium text-success">
        <CheckCircle2 className="size-5" aria-hidden />
        تسویه ثبت شد: {formatMoney(receipt.amount)} ({paymentMethodLabels[receipt.method]})
      </p>
      <ul className="space-y-1">
        {receipt.lines.map((line) => (
          <li key={line.id} className="flex justify-between gap-3">
            <span>{line.label}</span>
            <span>{formatMoney(line.amount)}</span>
          </li>
        ))}
      </ul>
      <p className="border-t pt-2 font-medium">
        {isPositiveMoney(receipt.remainingDebt)
          ? `بدهی باقی‌مانده: ${formatMoney(receipt.remainingDebt)}`
          : "بدهی این عضو صاف شد."}
      </p>
      <Button type="button" size="sm" variant="outline" onClick={onClose}>
        بستن
      </Button>
    </section>
  );
}
