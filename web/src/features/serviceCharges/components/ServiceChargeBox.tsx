import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
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
 * Only the possible actions are rendered, never a greyed-out button — the same decision as the
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
  const [open, setOpen] = useState<"amount" | "payment" | "void" | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const label = serviceChargeKindLabels[kind];

  function done(text: string) {
    setOpen(null);
    setNotice(text);
  }

  if (charge === undefined) {
    if (!visitIsOpen) {
      return null;
    }

    return (
      <div className="space-y-2">
        {notice !== null && <Alert role="status">{notice}</Alert>}
        {open === "amount" ? (
          <ServiceChargeAmountForm
            label={`مبلغ ${label}`}
            target={{ attendanceId, kind }}
            onDone={() => done(`مبلغ ${label} ثبت شد.`)}
            onCancel={() => setOpen(null)}
          />
        ) : (
          <Button size="sm" variant="outline" disabled={disabled} onClick={() => setOpen("amount")}>
            افزودن مبلغ {label}
          </Button>
        )}
      </div>
    );
  }

  const outstanding = subtractMoney(charge.amount, charge.netPaid);

  return (
    <div className="space-y-2 rounded-md border p-3">
      {notice !== null && <Alert role="status">{notice}</Alert>}

      <div className="flex flex-wrap items-center gap-3 text-sm">
        <span className="font-medium">{label}</span>
        <span>{formatMoney(charge.amount)}</span>
        <PaymentStatusBadge status={charge.paymentStatus} />
        {isPositiveMoney(outstanding) && (
          <span className="text-muted-foreground">مانده: {formatMoney(outstanding)}</span>
        )}
      </div>

      {open === null && (
        <div className="flex flex-wrap gap-2">
          {charge.canChangeAmount && (
            <Button
              size="sm"
              variant="outline"
              disabled={disabled}
              onClick={() => setOpen("amount")}
            >
              ویرایش مبلغ
            </Button>
          )}
          {isPositiveMoney(outstanding) && (
            <Button size="sm" disabled={disabled} onClick={() => setOpen("payment")}>
              ثبت پرداخت
            </Button>
          )}
          <Button
            size="sm"
            variant="destructive"
            disabled={disabled}
            onClick={() => setOpen("void")}
          >
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
          onCancel={() => setOpen(null)}
        />
      )}

      {open === "payment" && (
        <ServiceChargePaymentForm
          serviceChargeId={charge.id}
          onDone={() => done("پرداخت ثبت شد.")}
          onCancel={() => setOpen(null)}
        />
      )}

      {open === "void" && (
        <VoidServiceChargeForm
          serviceChargeId={charge.id}
          refundWarning={isPositiveMoney(charge.netPaid)}
          onDone={() => done(`مبلغ ${label} ابطال شد.`)}
          onCancel={() => setOpen(null)}
        />
      )}
    </div>
  );
}
