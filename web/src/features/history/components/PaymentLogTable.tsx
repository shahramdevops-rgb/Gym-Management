import { Badge } from "@/components/ui/badge";
import { paymentMethodLabels } from "@/features/payments/api";
import {
  SettlementHeading,
  SettlementItemLabel,
} from "@/features/payments/components/SettlementCells";
import { groupBySettlement, type SettlementSummary } from "@/features/payments/settlementGroups";
import { serviceChargeLabel } from "@/features/serviceCharges/api";
import { planLabel } from "@/features/subscriptions/planLabel";
import { emptyValue, formatDateTime, formatMoney } from "@/lib/format";

import { paymentSourceLabels, type HistoryPayment } from "../api";
import { WhoCell } from "./WhoCell";

/**
 * Every payment and refund, newest first, with whose money it was and who took it
 * (BUSINESS_RULES.md §12 History). A refund is its own row; a payment whose item was later
 * cancelled or voided keeps its row and says so, so the refund beside it explains itself. The rows
 * of one «تسویه یکجا» sit one step in under a heading with the one amount the desk took (§5).
 */
export function PaymentLogTable({ items }: { items: HistoryPayment[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">زمان</th>
            <th className="py-2 text-start font-medium">عضو</th>
            <th className="py-2 text-start font-medium">بابت</th>
            <th className="py-2 text-start font-medium">نوع</th>
            <th className="py-2 text-start font-medium">مبلغ</th>
            <th className="py-2 text-start font-medium">روش</th>
            <th className="py-2 text-start font-medium">ثبت توسط</th>
            <th className="py-2 text-start font-medium">توضیح</th>
          </tr>
        </thead>
        <tbody>
          {groupBySettlement(items).map((group) =>
            group.kind === "single" ? (
              <PaymentRow key={group.item.id} item={group.item} />
            ) : (
              <SettlementRows
                key={`${group.settlement.id}-${group.items[0]?.id}`}
                settlement={group.settlement}
                items={group.items}
              />
            ),
          )}
        </tbody>
      </table>
    </div>
  );
}

function PaymentRow({ item }: { item: HistoryPayment }) {
  return (
    <tr className="border-b">
      <td className="py-2">{formatDateTime(item.paidAt)}</td>
      <td className="py-2">
        <WhoCell
          memberId={item.memberId}
          memberFullName={item.memberFullName}
          guestName={item.guestName}
        />
      </td>
      <td className="py-2">
        <PaidFor item={item} />
      </td>
      <td className="py-2">
        <KindBadge kind={item.kind} />
      </td>
      <td className="py-2">{formatMoney(item.amount)}</td>
      <td className="py-2">{paymentMethodLabels[item.method]}</td>
      <td className="py-2">{item.receivedByFullName ?? emptyValue}</td>
      <td className="py-2 text-muted-foreground">
        {item.reason ?? item.referenceNumber ?? emptyValue}
      </td>
    </tr>
  );
}

/**
 * One handover of money: a heading with what every row shares (moment, whose, method, who took
 * it), then each item it paid, one step in, with only what differs.
 */
function SettlementRows({
  settlement,
  items,
}: {
  settlement: SettlementSummary;
  items: HistoryPayment[];
}) {
  const [first] = items;
  if (first === undefined) {
    return null;
  }

  return (
    <>
      <tr>
        <td className="pt-2 pb-1">{formatDateTime(first.paidAt)}</td>
        <td className="pt-2 pb-1">
          <WhoCell
            memberId={first.memberId}
            memberFullName={first.memberFullName}
            guestName={first.guestName}
          />
        </td>
        <td className="pt-2 pb-1">
          <SettlementHeading settlement={settlement} />
        </td>
        <td className="pt-2 pb-1">
          <KindBadge kind={first.kind} />
        </td>
        <td className="pt-2 pb-1 font-medium">{formatMoney(settlement.total)}</td>
        <td className="pt-2 pb-1">{paymentMethodLabels[first.method]}</td>
        <td className="pt-2 pb-1">{first.receivedByFullName ?? emptyValue}</td>
        <td className="pt-2 pb-1 text-muted-foreground">{first.referenceNumber ?? emptyValue}</td>
      </tr>
      {items.map((item, index) => {
        const last = index === items.length - 1;

        return (
          <tr key={item.id} className={last ? "border-b" : undefined}>
            <td />
            <td />
            <td className={last ? "pt-1 pb-2" : "py-1"}>
              <SettlementItemLabel>
                <PaidFor item={item} />
              </SettlementItemLabel>
            </td>
            <td />
            <td className="py-1 text-muted-foreground">{formatMoney(item.amount)}</td>
            <td />
            <td />
            <td />
          </tr>
        );
      })}
    </>
  );
}

function PaidFor({ item }: { item: HistoryPayment }) {
  return (
    <span className="flex flex-wrap items-center gap-2">
      {paidForLabel(item)}
      {item.targetUndone && (
        <Badge variant="outline">{item.source === "ServiceCharge" ? "ابطال شده" : "لغو شده"}</Badge>
      )}
    </span>
  );
}

function KindBadge({ kind }: { kind: HistoryPayment["kind"] }) {
  return (
    <Badge variant={kind === "Refund" ? "destructive" : "success"}>
      {kind === "Refund" ? "استرداد" : "پرداخت"}
    </Badge>
  );
}

/** The plan's numbers, the service's Persian name, or the cafe. */
function paidForLabel(item: HistoryPayment): string {
  switch (item.source) {
    case "Subscription":
      return item.subscriptionPlan === null
        ? paymentSourceLabels.Subscription
        : planLabel(item.subscriptionPlan);
    case "ServiceCharge":
      return item.serviceKind === null
        ? paymentSourceLabels.ServiceCharge
        : serviceChargeLabel(item.serviceKind, item.serviceDescription);
    case "CafeOrder":
      return paymentSourceLabels.CafeOrder;
  }
}
