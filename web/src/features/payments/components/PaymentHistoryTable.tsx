import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
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
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>تاریخ</TableHead>
          <TableHead>بابت</TableHead>
          <TableHead>نوع</TableHead>
          <TableHead>مبلغ</TableHead>
          <TableHead>روش</TableHead>
          <TableHead>توضیح</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
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
      </TableBody>
    </Table>
  );
}

function PaymentRow({ payment }: { payment: PaymentHistoryItem }) {
  return (
    <TableRow>
      <TableCell>{formatDateTime(payment.paidAt)}</TableCell>
      <TableCell>{paidForLabel(payment)}</TableCell>
      <TableCell>
        <KindBadge kind={payment.kind} />
      </TableCell>
      <TableCell>{formatMoney(payment.amount)}</TableCell>
      <TableCell>{paymentMethodLabels[payment.method]}</TableCell>
      <TableCell className="text-muted-foreground">
        {payment.reason ?? payment.referenceNumber ?? "—"}
      </TableCell>
    </TableRow>
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
      <TableRow>
        <TableCell className="pt-2 pb-1">{formatDateTime(first.paidAt)}</TableCell>
        <TableCell className="pt-2 pb-1">
          <SettlementHeading settlement={settlement} />
        </TableCell>
        <TableCell className="pt-2 pb-1">
          <KindBadge kind={first.kind} />
        </TableCell>
        <TableCell className="pt-2 pb-1 font-medium">{formatMoney(settlement.total)}</TableCell>
        <TableCell className="pt-2 pb-1">{paymentMethodLabels[first.method]}</TableCell>
        <TableCell className="pt-2 pb-1 text-muted-foreground">
          {first.referenceNumber ?? "—"}
        </TableCell>
      </TableRow>
      {payments.map((payment, index) => {
        const last = index === payments.length - 1;

        return (
          <TableRow key={payment.id} className={last ? undefined : "border-b-0"}>
            <TableCell />
            <TableCell className={last ? "pt-1 pb-2" : "py-1"}>
              <SettlementItemLabel>{paidForLabel(payment)}</SettlementItemLabel>
            </TableCell>
            <TableCell />
            <TableCell className="py-1 text-muted-foreground">
              {formatMoney(payment.amount)}
            </TableCell>
            <TableCell />
            <TableCell />
          </TableRow>
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
