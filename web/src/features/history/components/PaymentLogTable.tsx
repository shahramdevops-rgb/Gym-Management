import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { paymentMethodLabels } from "@/features/payments/api";
import {
  SettlementHeading,
  SettlementItemLabel,
} from "@/features/payments/components/SettlementCells";
import { groupBySettlement, type SettlementSummary } from "@/features/payments/settlementGroups";
import { serviceChargeLabel } from "@/features/serviceCharges/api";
import { planLabel } from "@/features/subscriptions/planLabel";
import { stateEdge } from "@/lib/edges";
import { emptyValue, formatDateTime, formatMoney } from "@/lib/format";
import { cn } from "@/lib/utils";

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
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>زمان</TableHead>
          <TableHead>عضو</TableHead>
          <TableHead>بابت</TableHead>
          <TableHead>نوع</TableHead>
          <TableHead>مبلغ</TableHead>
          <TableHead>روش</TableHead>
          <TableHead>ثبت توسط</TableHead>
          <TableHead>توضیح</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
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
      </TableBody>
    </Table>
  );
}

function PaymentRow({ item }: { item: HistoryPayment }) {
  return (
    <TableRow>
      <TableCell className="tone-row-edge">{formatDateTime(item.paidAt)}</TableCell>
      <TableCell label="عضو">
        <WhoCell
          memberId={item.memberId}
          memberFullName={item.memberFullName}
          guestName={item.guestName}
        />
      </TableCell>
      <TableCell label="بابت">
        <PaidFor item={item} />
      </TableCell>
      <TableCell label="نوع">
        <KindBadge kind={item.kind} />
      </TableCell>
      <TableCell label="مبلغ">{formatMoney(item.amount)}</TableCell>
      <TableCell label="روش">{paymentMethodLabels[item.method]}</TableCell>
      <TableCell label="ثبت توسط">{item.receivedByFullName ?? emptyValue}</TableCell>
      <TableCell label="توضیح" className="text-muted-foreground">
        {item.reason ?? item.referenceNumber ?? emptyValue}
      </TableCell>
    </TableRow>
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
      <TableRow joins="below">
        {/* The moment spans the heading and its items, so one start edge marks one handover. */}
        <TableCell rowSpan={items.length + 1} className="pt-2 pb-1 align-top tone-row-edge">
          {formatDateTime(first.paidAt)}
        </TableCell>
        <TableCell label="عضو" className="pt-2 pb-1">
          <WhoCell
            memberId={first.memberId}
            memberFullName={first.memberFullName}
            guestName={first.guestName}
          />
        </TableCell>
        <TableCell label="بابت" className="pt-2 pb-1">
          <SettlementHeading settlement={settlement} />
        </TableCell>
        <TableCell label="نوع" className="pt-2 pb-1">
          <KindBadge kind={first.kind} />
        </TableCell>
        <TableCell label="مبلغ" className="pt-2 pb-1 font-medium">
          {formatMoney(settlement.total)}
        </TableCell>
        <TableCell label="روش" className="pt-2 pb-1">
          {paymentMethodLabels[first.method]}
        </TableCell>
        <TableCell label="ثبت توسط" className="pt-2 pb-1">
          {first.receivedByFullName ?? emptyValue}
        </TableCell>
        <TableCell label="توضیح" className="pt-2 pb-1 text-muted-foreground">
          {first.referenceNumber ?? emptyValue}
        </TableCell>
      </TableRow>
      {items.map((item, index) => {
        const last = index === items.length - 1;

        return (
          <TableRow
            joins={last ? "above" : "both"}
            key={item.id}
            className={last ? undefined : "border-b-0"}
          >
            <TableCell />
            <TableCell className={last ? "pt-1 pb-2" : "py-1"}>
              <SettlementItemLabel>
                <PaidFor item={item} />
              </SettlementItemLabel>
            </TableCell>
            <TableCell />
            <TableCell className="py-1 text-muted-foreground">{formatMoney(item.amount)}</TableCell>
            <TableCell />
            <TableCell />
            <TableCell />
          </TableRow>
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

/**
 * Edged like the payment status tags (asked by the developer, 13.2i): no fill, a faint frame and a
 * thick end edge, green for money in, red for money given back.
 */
function KindBadge({ kind }: { kind: HistoryPayment["kind"] }) {
  return (
    <Badge
      variant="ghost"
      className={cn(kind === "Refund" ? stateEdge.destructive : stateEdge.success, "font-semibold")}
    >
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
