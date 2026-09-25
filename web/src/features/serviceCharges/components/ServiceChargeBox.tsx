import { useState } from "react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { formatMoney } from "@/lib/format";
import { isPositiveMoney, subtractMoney } from "@/lib/money";

import { serviceChargeKindLabels, type ServiceCharge, type ServiceChargeKind } from "../api";
import { ServiceChargeAmountForm } from "./ServiceChargeAmountForm";
import { ServiceChargePaymentForm } from "./ServiceChargePaymentForm";
import { VoidServiceChargeForm } from "./VoidServiceChargeForm";

interface ServiceChargeBoxProps {
  attendanceId: string;
  kind: ServiceChargeKind;
  /** The visit's live charge of this kind, or undefined when nothing has been charged yet. */
  charge: ServiceCharge | undefined;
  /** False once the visit is closed: nothing new can be charged to a visit that is over. */
  visitIsOpen: boolean;
  disabled?: boolean;
}

/**
 * The هوازی slot of one visit (BUSINESS_RULES.md §7 Gym services): an amount the front desk types
 * while the member is inside, and everything that can still be done to it afterwards.
 *
 * What is rendered here is only a summary — the amount and whether it is paid. Every form opens in
 * a dialog **over** the page instead of inside this element. The forms used to expand in place,
 * which was fine in the member profile and wrong everywhere else: this sits in a table cell on two
 * screens, and a form in a cell makes the row grow to three lines (task 6.5.2). A dialog also gives
 * the form the width it needs on a phone, which a narrow cell never could.
 *
 * Only the possible actions are offered, never a greyed-out button — the same decision as the
 * subscription history row in task 4.7. The rules decide which ones those are: the amount is
 * editable only while the visit is open and nothing has been paid (`canChangeAmount`, answered by
 * the API); a payment is offered for as long as anything is owed, whatever the visit's state,
 * because debt outlives the thing that created it (§5); and a void is always possible while the
 * charge stands, because it is the only correction left once money has moved.
 */
export function ServiceChargeBox({
  attendanceId,
  kind,
  charge,
  visitIsOpen,
  disabled = false,
}: ServiceChargeBoxProps) {
  const [open, setOpen] = useState<"amount" | "payment" | "void" | "actions" | null>(null);
  const [announcement, setAnnouncement] = useState<string | null>(null);

  const label = serviceChargeKindLabels[kind];

  /**
   * The dialog closes and the row shows the new amount, which is the confirmation a sighted user
   * needs. The live region says the same thing for a screen reader, which cannot see the row change.
   */
  function done(text: string) {
    setOpen(null);
    setAnnouncement(text);
  }

  /**
   * `aria-live` without `role="status"` on purpose. The role would make this element answer to
   * every "the page's status message" query — the pages this sits on have their own, one per
   * screen, and a hidden one per table row would shadow them. `aria-live="polite"` is what does
   * the announcing; the role only implies it.
   */
  const announcer = (
    <span aria-live="polite" aria-atomic="true" className="sr-only">
      {announcement}
    </span>
  );

  if (charge === undefined) {
    if (!visitIsOpen) {
      return null;
    }

    return (
      <>
        {announcer}
        <Button size="sm" variant="outline" disabled={disabled} onClick={() => setOpen("amount")}>
          افزودن مبلغ {label}
        </Button>

        <Dialog open={open === "amount"} onOpenChange={(next) => !next && setOpen(null)}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>افزودن مبلغ {label}</DialogTitle>
            </DialogHeader>
            <ServiceChargeAmountForm
              label={`مبلغ ${label}`}
              target={{ attendanceId, kind }}
              onDone={() => done(`مبلغ ${label} ثبت شد.`)}
              onCancel={() => setOpen(null)}
            />
          </DialogContent>
        </Dialog>
      </>
    );
  }

  const outstanding = subtractMoney(charge.amount, charge.netPaid);

  return (
    <>
      {announcer}

      {/* One line, whatever state the charge is in: the row must not grow (BUSINESS_RULES.md §7). */}
      <button
        type="button"
        disabled={disabled}
        onClick={() => setOpen("actions")}
        aria-label={`${label}: ${formatMoney(charge.amount)}`}
        className="flex items-center gap-2 rounded-md px-1 py-0.5 text-sm hover:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
      >
        <span>{formatMoney(charge.amount)}</span>
        <PaymentStatusBadge status={charge.paymentStatus} />
      </button>

      <Dialog open={open !== null} onOpenChange={(next) => !next && setOpen(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{label}</DialogTitle>
            <DialogDescription>
              <span>{formatMoney(charge.amount)}</span>
              {isPositiveMoney(outstanding) && (
                <span className="ms-2">مانده: {formatMoney(outstanding)}</span>
              )}
            </DialogDescription>
          </DialogHeader>

          {open === "actions" && (
            <div className="flex flex-wrap gap-2">
              {charge.canChangeAmount && (
                <Button size="sm" variant="outline" onClick={() => setOpen("amount")}>
                  ویرایش مبلغ
                </Button>
              )}
              {isPositiveMoney(outstanding) && (
                <Button size="sm" onClick={() => setOpen("payment")}>
                  ثبت پرداخت
                </Button>
              )}
              <Button size="sm" variant="destructive" onClick={() => setOpen("void")}>
                ابطال
              </Button>
            </div>
          )}

          {open === "amount" && (
            <ServiceChargeAmountForm
              label={`مبلغ ${label}`}
              target={{ id: charge.id }}
              initialAmount={String(charge.amount)}
              onDone={() => done(`مبلغ ${label} تغییر کرد.`)}
              onCancel={() => setOpen("actions")}
            />
          )}

          {open === "payment" && (
            <ServiceChargePaymentForm
              serviceChargeId={charge.id}
              onDone={() => done("پرداخت ثبت شد.")}
              onCancel={() => setOpen("actions")}
            />
          )}

          {open === "void" && (
            <VoidServiceChargeForm
              serviceChargeId={charge.id}
              refundWarning={isPositiveMoney(charge.netPaid)}
              onDone={() => done(`مبلغ ${label} ابطال شد.`)}
              onCancel={() => setOpen("actions")}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
