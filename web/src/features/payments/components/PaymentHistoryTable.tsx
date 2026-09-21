import { Badge } from "@/components/ui/badge";
import { formatDateTime, formatMoney } from "@/lib/format";

import { paymentMethodLabels, type PaymentHistoryItem } from "../api";

/** A member's payments and refunds across every subscription, newest first (task 4.5). */
export function PaymentHistoryTable({ payments }: { payments: PaymentHistoryItem[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">تاریخ</th>
            <th className="py-2 text-start font-medium">پلن</th>
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
              <td className="py-2">{payment.subscriptionPlanName}</td>
              <td className="py-2">
                <Badge variant={payment.kind === "Refund" ? "destructive" : "success"}>
                  {payment.kind === "Refund" ? "استرداد" : "پرداخت"}
                </Badge>
              </td>
              <td className="py-2">{formatMoney(Number(payment.amount))}</td>
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
