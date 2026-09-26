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

/** What the success step says once a form has gone through. */
interface Outcome {
  title: string;
  detail?: string;
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
 * Once a form goes through, the dialog stays open on a success step instead of vanishing, so the
 * desk sees that the amount was saved and what it was. The row changing behind the dialog was not
 * enough: at a busy desk nobody is watching the row. There is one dialog for every state of the
 * charge, because the success step outlives the state it started in — recording a first amount
 * turns "no charge" into "a charge", and a void turns it back.
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
  const [open, setOpen] = useState<"amount" | "payment" | "void" | "actions" | "done" | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const label = serviceChargeKindLabels[kind];

  function done(result: Outcome) {
    setOutcome(result);
    setOpen("done");
  }

  // Nothing charged on a finished visit: nothing to show and nothing to do — unless the dialog is
  // still saying that the charge was just voided.
  if (charge === undefined && !visitIsOpen && open === null) {
    return null;
  }

  const outstanding = charge === undefined ? "0" : subtractMoney(charge.amount, charge.netPaid);

  return (
    <>
      {charge === undefined ? (
        visitIsOpen && (
          <Button size="sm" variant="outline" disabled={disabled} onClick={() => setOpen("amount")}>
            مبلغ {label}
          </Button>
        )
      ) : (
        // One line, whatever state the charge is in: the row must not grow (BUSINESS_RULES.md §7).
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
      )}

      <Dialog open={open !== null} onOpenChange={(next) => !next && setOpen(null)}>
        <DialogContent>
          {open === "done" && outcome !== null && (
            <DialogSuccess
              title={outcome.title}
              description={outcome.detail}
              onClose={() => setOpen(null)}
            />
          )}

          {open !== "done" && charge === undefined && (
            <>
              <DialogHeader>
                <DialogTitle>ثبت مبلغ {label}</DialogTitle>
              </DialogHeader>
              <ServiceChargeAmountForm
                label={`مبلغ ${label}`}
                target={{ attendanceId, kind }}
                onDone={(amount) =>
                  done({ title: `مبلغ ${label} ثبت شد`, detail: formatMoney(amount) })
                }
                onCancel={() => setOpen(null)}
              />
            </>
          )}

          {open !== "done" && charge !== undefined && (
            <>
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
                  onDone={(amount) =>
                    done({ title: `مبلغ ${label} تغییر کرد`, detail: formatMoney(amount) })
                  }
                  onCancel={() => setOpen("actions")}
                />
              )}

              {open === "payment" && (
                <ServiceChargePaymentForm
                  serviceChargeId={charge.id}
                  onDone={() => done({ title: `پرداخت ${label} ثبت شد` })}
                  onCancel={() => setOpen("actions")}
                />
              )}

              {open === "void" && (
                <VoidServiceChargeForm
                  serviceChargeId={charge.id}
                  refundWarning={isPositiveMoney(charge.netPaid)}
                  onDone={() => done({ title: `مبلغ ${label} ابطال شد` })}
                  onCancel={() => setOpen("actions")}
                />
              )}
            </>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
