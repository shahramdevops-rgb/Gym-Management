import { Users } from "lucide-react";

import { formatMoney } from "@/lib/format";

import type { FinancialPeriod } from "../api";
import { ChartCard } from "./ChartCard";
import { Initial } from "./Initial";

/**
 * Who took how much money in the range (§12 *Financial report*, by staff member): every payment
 * they took and every refund they gave, the largest net first, as the API orders them.
 */
export function StaffMoneyTable({ rows }: { rows: FinancialPeriod["byStaff"] }) {
  return (
    <ChartCard
      title="دریافتی به تفکیک کارمند"
      description="پرداخت‌هایی که هر نفر گرفته و بازگشت‌هایی که داده است."
      icon={Users}
      tone="violet"
      empty={rows.length === 0}
    >
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
              <tr key={row.userId} className="border-b last:border-b-0 hover:bg-muted/50">
                <td className="py-2">
                  <span className="flex items-center gap-2">
                    <Initial name={row.fullName} tone="violet" />
                    {row.fullName}
                  </span>
                </td>
                <td className="py-2">{formatMoney(row.money.received)}</td>
                <td className="py-2">{formatMoney(row.money.refunded)}</td>
                <td className="py-2 font-bold">{formatMoney(row.money.net)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </ChartCard>
  );
}
