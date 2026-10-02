import { Badge } from "@/components/ui/badge";
import { paymentMethodLabels } from "@/features/payments/api";
import { serviceChargeKindLabels } from "@/features/serviceCharges/api";
import { planLabel } from "@/features/subscriptions/planLabel";
import { emptyValue, formatDateTime, formatMoney } from "@/lib/format";

import { paymentSourceLabels, type HistoryPayment } from "../api";
import { WhoCell } from "./WhoCell";

/**
 * Every payment and refund, newest first, with whose money it was and who took it
 * (BUSINESS_RULES.md §12 History). A refund is its own row; a payment whose item was later
 * cancelled or voided keeps its row and says so, so the refund beside it explains itself.
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
          {items.map((item) => (
            <tr key={item.id} className="border-b">
              <td className="py-2">{formatDateTime(item.paidAt)}</td>
              <td className="py-2">
                <WhoCell
                  memberId={item.memberId}
                  memberFullName={item.memberFullName}
                  guestName={item.guestName}
                />
              </td>
              <td className="py-2">
                <span className="flex flex-wrap items-center gap-2">
                  {paidForLabel(item)}
                  {item.targetUndone && (
                    <Badge variant="outline">
                      {item.source === "ServiceCharge" ? "ابطال شده" : "لغو شده"}
                    </Badge>
                  )}
                </span>
              </td>
              <td className="py-2">
                <Badge variant={item.kind === "Refund" ? "destructive" : "success"}>
                  {item.kind === "Refund" ? "استرداد" : "پرداخت"}
                </Badge>
              </td>
              <td className="py-2">{formatMoney(item.amount)}</td>
              <td className="py-2">{paymentMethodLabels[item.method]}</td>
              <td className="py-2">{item.receivedByFullName ?? emptyValue}</td>
              <td className="py-2 text-muted-foreground">
                {item.reason ?? item.referenceNumber ?? emptyValue}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
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
        : serviceChargeKindLabels[item.serviceKind];
    case "CafeOrder":
      return paymentSourceLabels.CafeOrder;
  }
}
