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
import {
  PurchaseTile,
  PurchaseTileSummary,
  type PurchaseTileKind,
} from "@/features/attendance/components/PurchaseTile";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { addMoney, isPositiveMoney, subtractMoney } from "@/lib/money";

import {
  serviceChargeKindLabels,
  serviceChargeLabel,
  type SaleKind,
  type ServiceCharge,
} from "../api";
import { ServiceChargeAmountForm } from "./ServiceChargeAmountForm";
import { ServiceChargePaymentForm } from "./ServiceChargePaymentForm";
import { ShopSaleForm } from "./ShopSaleForm";
import { VoidServiceChargeForm } from "./VoidServiceChargeForm";

interface SaleBoxProps {
  attendanceId: string;
  /** فروشگاه, آنالیز or متفرقه: which tile this is and which source its sales are filed under. */
  kind: SaleKind;
  memberName: string;
  /**
   * A guest's visit (BUSINESS_RULES.md §7 *Guest visit*): the sale is under the guest's name and is
   * paid before they leave, not put on an account, and the box says so.
   */
  isGuest?: boolean;
  /** The visit's standing sales of this kind (voided ones never arrive). */
  sales: ServiceCharge[];
}

type Step =
  | { kind: "add" }
  | { kind: "list" }
  | { kind: "pay"; sale: ServiceCharge }
  | { kind: "void"; sale: ServiceCharge }
  | { kind: "done"; title: string; detail?: string };

const tileKinds: Record<SaleKind, PurchaseTileKind> = {
  Miscellaneous: "shop",
  Analysis: "analysis",
  Other: "other",
};

/**
 * The «فروشگاه», «آنالیز» or «متفرقه» tile of one visit, beside هوازی and بوفه (BUSINESS_RULES.md §7
 * *Sale at the desk*): something sold at the desk that has no product of its own. A visit may have
 * any number of them, so the tile shows their total like the cafe's, and the list opens in a dialog.
 * فروشگاه takes one or more named items (`ShopSaleForm`); آنالیز and متفرقه are a single price
 * (`ServiceChargeAmountForm`, the هوازی form). Either way no money is taken when it is recorded:
 * it goes on the member's account (decided with the developer, 1405/07/12), or on a guest's visit
 * under their name, to be paid before they leave (task 6.5.31).
 *
 * A sale is never edited. A mistake is voided with a reason, which gives back whatever was paid,
 * and the sale is entered again — the rule a cafe order follows. A sale left on the account can be
 * paid from here, or with everything else in «تسویه یکجا».
 */
export function SaleBox({ attendanceId, kind, memberName, isGuest = false, sales }: SaleBoxProps) {
  const [step, setStep] = useState<Step | null>(null);
  const label = serviceChargeKindLabels[kind];
  // Where the sale goes, in the cafe tile's words.
  const whose = isGuest ? `به نام مهمان، ${memberName}` : `به حساب ${memberName}`;
  const where = isGuest
    ? "به نام مهمان ثبت می‌شود و پیش از خروج پرداخت می‌شود."
    : "به حساب عضو ثبت می‌شود.";

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
      <PurchaseTile
        kind={tileKinds[kind]}
        onClick={() => setStep(sales.length === 0 ? { kind: "add" } : { kind: "list" })}
        ariaLabel={sales.length === 0 ? label : `${label}: ${formatMoney(total)}`}
        summary={
          sales.length > 0 && (
            <PurchaseTileSummary
              amount={formatMoney(total)}
              badge={<PaymentStatusBadge status={status} />}
            />
          )
        }
      />

      <Dialog open={step !== null} onOpenChange={(next) => !next && setStep(null)}>
        <DialogContent>
          {step?.kind === "done" && (
            <DialogSuccess
              title={step.title}
              description={step.detail}
              onClose={() => setStep(null)}
            />
          )}

          {step !== null && step.kind !== "done" && (
            <DialogHeader>
              <DialogTitle>
                {label} — {memberName}
              </DialogTitle>
              <DialogDescription>
                {step.kind === "list"
                  ? `فروش‌های ${label} در این مراجعه: ${formatMoney(total)}`
                  : step.kind === "add"
                    ? kind !== "Miscellaneous"
                      ? `مبلغ ${label} را وارد کنید. ${where}`
                      : `نام، تعداد و قیمت هر کالا را وارد کنید. مبلغ ${where}`
                    : saleLine(step.sale)}
              </DialogDescription>
            </DialogHeader>
          )}

          {step?.kind === "list" && (
            <div className="space-y-3">
              <ul className="divide-y rounded-md border" aria-label={`فروش‌های ${label}`}>
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

          {step?.kind === "add" && kind !== "Miscellaneous" && (
            <ServiceChargeAmountForm
              label={`مبلغ ${label}`}
              target={{ attendanceId, kind }}
              onDone={(amount) =>
                setStep({
                  kind: "done",
                  title: `${label} ثبت شد`,
                  detail: `${formatMoney(amount)}، ${whose}`,
                })
              }
              onCancel={back}
            />
          )}

          {step?.kind === "add" && kind === "Miscellaneous" && (
            <ShopSaleForm
              attendanceId={attendanceId}
              isGuest={isGuest}
              onDone={(saved) =>
                setStep({
                  kind: "done",
                  title: `فروش ${label} ثبت شد`,
                  detail: `${saved.map(saleLine).join("، ")} — ${formatMoney(
                    addMoney(...saved.map((sale) => sale.amount)),
                  )}، ${whose}`,
                })
              }
              onCancel={back}
            />
          )}

          {step?.kind === "pay" && (
            <ServiceChargePaymentForm
              serviceChargeId={step.sale.id}
              onDone={() => setStep({ kind: "done", title: `پرداخت ${label} ثبت شد` })}
              onCancel={back}
            />
          )}

          {step?.kind === "void" && (
            <VoidServiceChargeForm
              serviceChargeId={step.sale.id}
              refundWarning={isPositiveMoney(step.sale.netPaid)}
              onDone={() => setStep({ kind: "done", title: `فروش ${label} ابطال شد` })}
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
  const name = sale.description ?? serviceChargeLabel(sale.kind, null);
  const quantity = sale.quantity === null ? 1 : Number(sale.quantity);
  if (quantity === 1 || sale.unitPrice === null) {
    return name;
  }

  return `${name} × ${toPersianDigits(quantity)} (هر عدد ${formatMoney(sale.unitPrice)})`;
}
