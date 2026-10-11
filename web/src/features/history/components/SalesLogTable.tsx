import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { serviceChargeLabel } from "@/features/serviceCharges/api";
import { planLabel } from "@/features/subscriptions/planLabel";
import { emptyValue, formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";

import { saleSourceLabels, type HistorySale } from "../api";
import { WhoCell } from "./WhoCell";

/**
 * Everything the gym sold, newest first, with what has been paid on it (BUSINESS_RULES.md §12
 * Sales in the history). A cancelled plan or cafe order and a voided charge stay on the list,
 * tagged beside what was sold, with the reason under it: they owe nothing, so their payment cell is
 * empty. No status column: on every other row it would only say «—» (roadmap 6.5.34).
 */
export function SalesLogTable({ items }: { items: HistorySale[] }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>زمان</TableHead>
          <TableHead>مشتری</TableHead>
          <TableHead>بابت</TableHead>
          <TableHead>مبلغ</TableHead>
          <TableHead>پرداخت</TableHead>
          <TableHead>ثبت توسط</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((item) => (
          <TableRow key={`${item.source}-${item.id}`}>
            <TableCell>{formatDateTime(item.soldAt)}</TableCell>
            <TableCell>
              <WhoCell
                memberId={item.memberId}
                memberFullName={item.memberFullName}
                guestName={item.guestName}
              />
            </TableCell>
            <TableCell>
              <div className="flex flex-wrap items-center gap-1.5">
                <span>{saleLabel(item)}</span>
                {item.undoneAt !== null && (
                  <Badge variant="outline">
                    {item.source === "Subscription" || item.source === "CafeOrder"
                      ? "لغو شده"
                      : "ابطال شده"}
                  </Badge>
                )}
              </div>
              {item.undoneAt !== null && item.undoReason !== null && (
                <p className="text-muted-foreground">{item.undoReason}</p>
              )}
            </TableCell>
            <TableCell>{formatMoney(item.amount)}</TableCell>
            <TableCell>
              {item.undoneAt === null ? (
                <PaymentStatusBadge status={item.paymentStatus} />
              ) : (
                emptyValue
              )}
            </TableCell>
            <TableCell>{item.recordedByFullName ?? emptyValue}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * What was sold: «پلن ۱۲ جلسه - ۳۰ روزه» or «پلن تک‌جلسه‌ای», the service's name (with a فروشگاه
 * item's name and how many), or the cafe with what the order held.
 */
function saleLabel(item: HistorySale): string {
  switch (item.source) {
    case "Subscription":
      return item.plan === null
        ? saleSourceLabels.Subscription
        : `${saleSourceLabels.Subscription} ${planLabel(item.plan)}`;
    case "CafeOrder": {
      const lines = (item.cafeItems ?? []).map((line) =>
        Number(line.quantity) > 1
          ? `${line.productName} × ${toPersianDigits(line.quantity)}`
          : line.productName,
      );
      return lines.length === 0
        ? saleSourceLabels.CafeOrder
        : `${saleSourceLabels.CafeOrder}: ${lines.join("، ")}`;
    }
    default: {
      const label = serviceChargeLabel(item.source, item.description);
      return item.quantity !== null && Number(item.quantity) > 1
        ? `${label} × ${toPersianDigits(item.quantity)}`
        : label;
    }
  }
}
