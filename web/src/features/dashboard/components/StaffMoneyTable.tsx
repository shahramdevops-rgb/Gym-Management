import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatMoney } from "@/lib/format";

import type { FinancialPeriod } from "../api";

/**
 * Who took how much money in the range (§12 *Financial report*, by staff member): every payment
 * they took and every refund they gave, the largest net first, as the API orders them. The first
 * step towards the shift handover (roadmap 9.5).
 */
export function StaffMoneyTable({ rows }: { rows: FinancialPeriod["byStaff"] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>دریافتی به تفکیک کارمند</CardTitle>
        <CardDescription>پرداخت‌هایی که هر نفر گرفته و بازگشت‌هایی که داده است.</CardDescription>
      </CardHeader>
      <CardContent>
        {rows.length === 0 ? (
          <p className="py-8 text-center text-sm text-muted-foreground">در این بازه چیزی نیست.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-muted-foreground">
                  <th className="py-2 text-start font-medium">کارمند</th>
                  <th className="py-2 text-start font-medium">دریافتی</th>
                  <th className="py-2 text-start font-medium">بازگشت</th>
                  <th className="py-2 text-start font-medium">خالص</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.userId} className="border-b">
                    <td className="py-2">{row.fullName}</td>
                    <td className="py-2">{formatMoney(row.money.received)}</td>
                    <td className="py-2">{formatMoney(row.money.refunded)}</td>
                    <td className="py-2 font-medium">{formatMoney(row.money.net)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
