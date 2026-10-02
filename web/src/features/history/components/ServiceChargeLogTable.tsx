import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { emptyValue, formatDateTime, formatMoney } from "@/lib/format";

import type { HistoryServiceCharge } from "../api";

/**
 * Every هوازی charge, newest first, with who recorded it (BUSINESS_RULES.md §12 History). A voided
 * charge stays on the list, marked, with its reason and who voided it: the desk's own boxes leave
 * voided charges out, which is why this is the place to find one.
 */
export function ServiceChargeLogTable({ items }: { items: HistoryServiceCharge[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">زمان</th>
            <th className="py-2 text-start font-medium">عضو</th>
            <th className="py-2 text-start font-medium">مبلغ</th>
            <th className="py-2 text-start font-medium">پرداخت</th>
            <th className="py-2 text-start font-medium">ثبت توسط</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-b">
              <td className="py-2">{formatDateTime(item.createdAt)}</td>
              <td className="py-2">
                <Link to={paths.member(item.memberId)} className="font-medium hover:underline">
                  {item.memberFullName}
                </Link>
              </td>
              <td className="py-2">{formatMoney(item.amount)}</td>
              <td className="py-2">
                {item.voidedAt === null ? (
                  <PaymentStatusBadge status={item.paymentStatus} />
                ) : (
                  emptyValue
                )}
              </td>
              <td className="py-2">{item.recordedByFullName ?? emptyValue}</td>
              <td className="py-2">
                {item.voidedAt === null ? (
                  emptyValue
                ) : (
                  <div className="space-y-1">
                    <Badge variant="outline">ابطال شده</Badge>
                    <p className="text-muted-foreground">
                      {item.voidReason}
                      {item.voidedByFullName !== null && ` — ${item.voidedByFullName}`}
                    </p>
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
