import { useState } from "react";

import { DialogSuccess } from "@/components/DialogSuccess";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { addMoney, isPositiveMoney, subtractMoney } from "@/lib/money";

import type { ServiceCharge } from "../api";
import { MiscellaneousSaleForm } from "./MiscellaneousSaleForm";
import { ServiceChargePaymentForm } from "./ServiceChargePaymentForm";
import { VoidServiceChargeForm } from "./VoidServiceChargeForm";

interface MiscellaneousSaleBoxProps {
  attendanceId: string;
  memberName: string;
  /** The visit's standing miscellaneous sales (voided ones never arrive). */
  sales: ServiceCharge[];
}

type Step =
  | { kind: "add" }
  | { kind: "list" }
  | { kind: "pay"; sale: ServiceCharge }
  | { kind: "void"; sale: ServiceCharge }
  | { kind: "done"; title: string; detail?: string };

/**
 * The «متفرقه» slot of one visit, beside هوازی and بوفه (BUSINESS_RULES.md §7 *Miscellaneous
 * sale*): something sold at the desk that has no product of its own. A visit may have any number
 * of them, so the slot shows their total like the cafe's, and the list opens in a dialog.
 *
 * A sale is never edited. A mistake is voided with a reason, which gives back whatever was paid,
 * and the sale is entered again — the rule a cafe order follows. A sale left on the account can be
 * paid from here, or with everything else in «تسویه یکجا».
 */
export function MiscellaneousSaleBox({ attendanceId, memberName, sales }: MiscellaneousSaleBoxProps) {
  const [step, setStep] = useState<Step | null>(null);

  const total = addMoney(...sales.map((sale) => sale.amount));
  const netPaid = addMoney(...sales.map((sale) => sale.netPaid));
  const outstanding = subtractMoney(total, netPaid);
  const status = !isPositiveMoney(outstanding)
    ? "Paid"
    : isPositiveMoney(netPaid)
      ? "Partial"
      : "Unpaid";

  // After a step, back to the list while there is one to go back to.
  const back = () => setStep(sales.length === 0 ? null : { kind: "list" });

  return (
    <>
      {sales.length === 0 ? (
        <Button size="sm" variant="outline" onClick={() => setStep({ kind: "add" })}>
          فروش متفرقه
        </Button>
      ) : (
        <button
          type="button"
          onClick={() => setStep({ kind: "list" })}
          aria-label={`متفرقه: ${formatMoney(total)}`}
          className="flex items-center gap-2 rounded-md px-1 py-0.5 text-sm hover:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <span>{formatMoney(total)}</span>
          <PaymentStatusBadge status={status} />
        </button>
      )}

      <Dialog open={step !== null} onOpenChange={(next) => !next && setStep(null)}>
        <DialogContent>
          {step?.kind === "done" && (
            <DialogSuccess title={step.title} description={step.detail} onClose={() => setStep(null)} />
          )}

          {step !== null && step.kind !== "done" && (
            <DialogHeader>
              <DialogTitle>متفرقه — {memberName}</DialogTitle>
              <DialogDescription>
                {step.kind === "list"
                  ? `فروش‌های این مراجعه: ${formatMoney(total)}`
                  : step.kind === "add"
                    ? "نام، تعداد و قیمت را وارد کنید و نوع پرداخت را انتخاب کنید."
                    : saleLine(step.sale)}
              </DialogDescription>
            </DialogHeader>
          )}

          {step?.kind === "list" && (
            <div className="space-y-3">
              <ul className="divide-y rounded-md border" aria-label="فروش‌های متفرقه">
                {sales.map((sale) => (
                  <li key={sale.id} className="flex flex-wrap items-center gap-2 p-3 text-sm">
                    <span className="flex-1">{saleLine(sale)}</span>
                    <span>{formatMoney(sale.amount)}</span>
                    <PaymentStatusBadge status={sale.paymentStatus} />
                    <span className="flex gap-1">
                      {isPositiveMoney(subtractMoney(sale.amount, sale.netPaid)) && (
                        <Button size="sm" onClick={() => setStep({ kind: "pay", sale })}>
                          ثبت پرداخت
                        </Button>
                      )}
                      <Button
                        size="sm"
                        variant="destructive"
                        onClick={() => setStep({ kind: "void", sale })}
                      >
                        ابطال
                      </Button>
                    </span>
                  </li>
                ))}
              </ul>
              <Button size="sm" onClick={() => setStep({ kind: "add" })}>
                فروش دیگر
              </Button>
            </div>
          )}

          {step?.kind === "add" && (
            <MiscellaneousSaleForm
              attendanceId={attendanceId}
              onDone={(sale) =>
                setStep({
                  kind: "done",
                  title: "فروش متفرقه ثبت شد",
                  detail: `${saleLine(sale)} — ${formatMoney(sale.amount)}، ${paidHow(sale, memberName)}`,
                })
              }
              onCancel={back}
            />
          )}

          {step?.kind === "pay" && (
            <ServiceChargePaymentForm
              serviceChargeId={step.sale.id}
              onDone={() => setStep({ kind: "done", title: "پرداخت متفرقه ثبت شد" })}
              onCancel={back}
            />
          )}

          {step?.kind === "void" && (
            <VoidServiceChargeForm
              serviceChargeId={step.sale.id}
              refundWarning={isPositiveMoney(step.sale.netPaid)}
              onDone={() => setStep({ kind: "done", title: "فروش متفرقه ابطال شد" })}
              onCancel={back}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

/** «دستکش × ۲ (هر عدد ۱۵۰٬۰۰۰ تومان)». */
function saleLine(sale: ServiceCharge): string {
  const name = sale.description ?? "متفرقه";
  const quantity = sale.quantity === null ? 1 : Number(sale.quantity);
  if (quantity === 1 || sale.unitPrice === null) {
    return name;
  }

  return `${name} × ${toPersianDigits(quantity)} (هر عدد ${formatMoney(sale.unitPrice)})`;
}

/** What the success step says about the money: the method it came in, or the account it went on. */
function paidHow(sale: ServiceCharge, memberName: string): string {
  if (!isPositiveMoney(sale.netPaid)) {
    return `به حساب ${memberName}`;
  }

  return sale.paymentStatus === "Paid" ? "پرداخت شد" : "بخشی پرداخت شد";
}
