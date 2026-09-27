import { Badge } from "@/components/ui/badge";
import { serviceChargeKindLabels } from "@/features/serviceCharges/api";
import { planLabel } from "@/features/subscriptions/planLabel";
import { formatDateTime, formatMoney } from "@/lib/format";

import { paymentMethodLabels, type PaymentHistoryItem } from "../api";

/**
 * A member's payments and refunds across everything they have paid for — subscriptions, gym
 * services and cafe orders — newest first (task 4.5, extended in 5.7 and 7.3).
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
          {payments.map((payment) => (
            <tr key={payment.id} className="border-b">
              <td className="py-2">{formatDateTime(payment.paidAt)}</td>
              <td className="py-2">{paidForLabel(payment)}</td>
              <td className="py-2">
                <Badge variant={payment.kind === "Refund" ? "destructive" : "success"}>
                  {payment.kind === "Refund" ? "استرداد" : "پرداخت"}
                </Badge>
              </td>
              <td className="py-2">{formatMoney(payment.amount)}</td>
              <td className="py-2">{paymentMethodLabels[payment.method]}</td>
              <td className="py-2 text-muted-foreground">
                {payment.reason ?? payment.referenceNumber ?? "—"}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
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
      return payment.serviceKind === null ? "خدمات" : serviceChargeKindLabels[payment.serviceKind];
    case "CafeOrder":
      return "کافه";
  }
}
