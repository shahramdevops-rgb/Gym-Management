import { Badge } from "@/components/ui/badge";
import { serviceChargeLabel } from "@/features/serviceCharges/api";
import { planLabel } from "@/features/subscriptions/planLabel";
import { formatDateTime, formatMoney } from "@/lib/format";

import { paymentMethodLabels, type PaymentHistoryItem } from "../api";
import { groupBySettlement, type SettlementSummary } from "../settlementGroups";
import { SettlementHeading, SettlementItemLabel } from "./SettlementCells";

/**
 * A member's payments and refunds across everything they have paid for — subscriptions, gym
 * services and cafe orders — newest first (task 4.5, extended in 5.7 and 7.3). The rows of one
 * «تسویه یکجا» sit one step in under a heading with the one amount the desk took
 * (BUSINESS_RULES.md §5).
 */
export function PaymentHistoryTable({ payments }: { payments: PaymentHistoryItem[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">تاریخ</th>
            <th className="py-2 text-start font-medium">بابت</th>
            <th className="py-2 text-start font-medium">نوع</th>
            <th className="py-2 text-start font-medium">مبلغ</th>
            <th className="py-2 text-start font-medium">روش</th>
            <th className="py-2 text-start font-medium">توضیح</th>
          </tr>
        </thead>
        <tbody>
          {groupBySettlement(payments).map((group) =>
            group.kind === "single" ? (
              <PaymentRow key={group.item.id} payment={group.item} />
            ) : (
              <SettlementRows
                key={`${group.settlement.id}-${group.items[0]?.id}`}
                settlement={group.settlement}
                payments={group.items}
              />
            ),
          )}
        </tbody>
      </table>
    </div>
  );
}

function PaymentRow({ payment }: { payment: PaymentHistoryItem }) {
  return (
    <tr className="border-b">
      <td className="py-2">{formatDateTime(payment.paidAt)}</td>
      <td className="py-2">{paidForLabel(payment)}</td>
      <td className="py-2">
        <KindBadge kind={payment.kind} />
      </td>
      <td className="py-2">{formatMoney(payment.amount)}</td>
      <td className="py-2">{paymentMethodLabels[payment.method]}</td>
      <td className="py-2 text-muted-foreground">
        {payment.reason ?? payment.referenceNumber ?? "—"}
      </td>
    </tr>
  );
}

/**
 * One handover of money: a heading with what every row shares, then each item it paid, one step
 * in, with only what differs.
 */
function SettlementRows({
  settlement,
  payments,
}: {
  settlement: SettlementSummary;
  payments: PaymentHistoryItem[];
}) {
  const [first] = payments;
  if (first === undefined) {
    return null;
  }

  return (
    <>
      <tr>
        <td className="pt-2 pb-1">{formatDateTime(first.paidAt)}</td>
        <td className="pt-2 pb-1">
          <SettlementHeading settlement={settlement} />
        </td>
        <td className="pt-2 pb-1">
          <KindBadge kind={first.kind} />
        </td>
        <td className="pt-2 pb-1 font-medium">{formatMoney(settlement.total)}</td>
        <td className="pt-2 pb-1">{paymentMethodLabels[first.method]}</td>
        <td className="pt-2 pb-1 text-muted-foreground">{first.referenceNumber ?? "—"}</td>
      </tr>
      {payments.map((payment, index) => {
        const last = index === payments.length - 1;

        return (
          <tr key={payment.id} className={last ? "border-b" : undefined}>
            <td />
            <td className={last ? "pt-1 pb-2" : "py-1"}>
              <SettlementItemLabel>{paidForLabel(payment)}</SettlementItemLabel>
            </td>
            <td />
            <td className="py-1 text-muted-foreground">{formatMoney(payment.amount)}</td>
            <td />
            <td />
          </tr>
        );
      })}
    </>
  );
}

function KindBadge({ kind }: { kind: PaymentHistoryItem["kind"] }) {
  return (
    <Badge variant={kind === "Refund" ? "destructive" : "success"}>
      {kind === "Refund" ? "استرداد" : "پرداخت"}
    </Badge>
  );
}

/**
 * What the money went against: the plan's name now, the Persian word for the service, or the
 * cafe. A `switch` over every kind, so TypeScript complains if a fourth one is added unlabelled.
 */
function paidForLabel(payment: PaymentHistoryItem): string {
  switch (payment.targetKind) {
    case "Subscription":
      return payment.subscriptionPlan === null ? "—" : planLabel(payment.subscriptionPlan);
    case "ServiceCharge":
      return serviceChargeLabel(payment.serviceKind, payment.serviceDescription);
    case "CafeOrder":
      return "کافه";
  }
}
