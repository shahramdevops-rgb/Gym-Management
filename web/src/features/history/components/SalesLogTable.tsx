import { Badge } from "@/components/ui/badge";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { serviceChargeLabel } from "@/features/serviceCharges/api";
import { planLabel } from "@/features/subscriptions/planLabel";
import { emptyValue, formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";

import { saleSourceLabels, type HistorySale } from "../api";
import { WhoCell } from "./WhoCell";

/**
 * Everything the gym sold, newest first, with what has been paid on it (BUSINESS_RULES.md §12
 * Sales in the history). A cancelled plan or cafe order and a voided charge stay on the list,
 * marked, with the reason: they owe nothing, so their payment cell is empty.
 */
export function SalesLogTable({ items }: { items: HistorySale[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">زمان</th>
            <th className="py-2 text-start font-medium">مشتری</th>
            <th className="py-2 text-start font-medium">بابت</th>
            <th className="py-2 text-start font-medium">مبلغ</th>
            <th className="py-2 text-start font-medium">پرداخت</th>
            <th className="py-2 text-start font-medium">ثبت توسط</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={`${item.source}-${item.id}`} className="border-b">
              <td className="py-2">{formatDateTime(item.soldAt)}</td>
              <td className="py-2">
                <WhoCell
                  memberId={item.memberId}
                  memberFullName={item.memberFullName}
                  guestName={item.guestName}
                />
              </td>
              <td className="py-2">{saleLabel(item)}</td>
              <td className="py-2">{formatMoney(item.amount)}</td>
              <td className="py-2">
                {item.undoneAt === null ? (
                  <PaymentStatusBadge status={item.paymentStatus} />
                ) : (
                  emptyValue
                )}
              </td>
              <td className="py-2">{item.recordedByFullName ?? emptyValue}</td>
              <td className="py-2">
                {item.undoneAt === null ? (
                  emptyValue
                ) : (
                  <div className="space-y-1">
                    <Badge variant="outline">
                      {item.source === "Subscription" || item.source === "CafeOrder"
                        ? "لغو شده"
                        : "ابطال شده"}
                    </Badge>
                    {item.undoReason !== null && (
                      <p className="text-muted-foreground">{item.undoReason}</p>
                    )}
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
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
