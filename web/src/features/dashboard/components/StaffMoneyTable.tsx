import { Users } from "lucide-react";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
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
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>کارمند</TableHead>
            <TableHead>دریافتی</TableHead>
            <TableHead>بازگشت</TableHead>
            <TableHead>خالص</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={row.userId}>
              {/* Edged at its start in the card's tone, like every table row (13.2i). */}
              <TableCell className="tone-row-edge">
                <span className="flex items-center gap-2">
                  <Initial name={row.fullName} tone="violet" />
                  {row.fullName}
                </span>
              </TableCell>
              <TableCell label="دریافتی">{formatMoney(row.money.received)}</TableCell>
              <TableCell label="بازگشت">{formatMoney(row.money.refunded)}</TableCell>
              <TableCell label="خالص" className="font-bold">
                {formatMoney(row.money.net)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </ChartCard>
  );
}
