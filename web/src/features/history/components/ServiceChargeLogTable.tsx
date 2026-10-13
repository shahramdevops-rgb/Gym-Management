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
import { emptyValue, formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";

import type { HistoryServiceCharge } from "../api";
import { WhoCell } from "./WhoCell";

/**
 * Every هوازی charge and sale (فروشگاه, آنالیز, متفرقه), newest first, with what it was and who
 * recorded it (BUSINESS_RULES.md §12 History); a guest's under their name, marked «مهمان». A voided charge
 * stays on the list, marked, with its reason and who voided it: the desk's own boxes leave voided
 * charges out, which is why this is the place to find one.
 */
export function ServiceChargeLogTable({ items }: { items: HistoryServiceCharge[] }) {
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
          <TableHead>وضعیت</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.id}>
            <TableCell className="tone-row-edge">{formatDateTime(item.createdAt)}</TableCell>
            <TableCell label="مشتری">
              <WhoCell
                memberId={item.memberId}
                memberFullName={item.memberFullName}
                guestName={item.guestName}
              />
            </TableCell>
            <TableCell label="بابت">
              {serviceChargeLabel(item.kind, item.description)}
              {item.quantity !== null && Number(item.quantity) > 1 && (
                <span className="text-muted-foreground"> × {toPersianDigits(item.quantity)}</span>
              )}
            </TableCell>
            <TableCell label="مبلغ">{formatMoney(item.amount)}</TableCell>
            <TableCell label="پرداخت">
              {item.voidedAt === null ? (
                <PaymentStatusBadge status={item.paymentStatus} />
              ) : (
                emptyValue
              )}
            </TableCell>
            <TableCell label="ثبت توسط">{item.recordedByFullName ?? emptyValue}</TableCell>
            <TableCell label="وضعیت">
              {item.voidedAt === null ? (
                emptyValue
              ) : (
                <div className="space-y-1">
                  <Badge variant="destructive">ابطال شده</Badge>
                  <p className="text-muted-foreground">
                    {item.voidReason}
                    {item.voidedByFullName !== null && ` — ${item.voidedByFullName}`}
                  </p>
                </div>
              )}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
