import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
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

/**
 * Who took how much money in the range (§12 *Financial report*, by staff member): every payment
 * they took and every refund they gave, the largest net first, as the API orders them.
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
                  <TableCell>{row.fullName}</TableCell>
                  <TableCell>{formatMoney(row.money.received)}</TableCell>
                  <TableCell>{formatMoney(row.money.refunded)}</TableCell>
                  <TableCell className="font-medium">{formatMoney(row.money.net)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
