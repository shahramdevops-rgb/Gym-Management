import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ServiceChargeBox } from "@/features/serviceCharges/components/ServiceChargeBox";
import { emptyValue, formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";

import type { Attendance } from "../api";
import { birthdayVisitLabel, cardioOnlyLabel } from "../holder";

/** A member's own visits, most recent first: cancelled and auto-closed ones included, marked. */
export function AttendanceHistoryTable({ items }: { items: Attendance[] }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>ورود</TableHead>
          <TableHead>خروج</TableHead>
          <TableHead>کمد</TableHead>
          <TableHead>هوازی</TableHead>
          <TableHead>وضعیت</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.id}>
            {/* Edged at its start in the page's tone, like the history's rows (13.2j). */}
            <TableCell className="tone-row-edge">{formatDateTime(item.checkedInAt)}</TableCell>
            <TableCell label="خروج">{formatDateTime(item.checkedOutAt)}</TableCell>
            <TableCell label="کمد">
              {item.lockerNumber === null ? emptyValue : toPersianDigits(item.lockerNumber)}
            </TableCell>
            <TableCell label="هوازی">
              <CardioCell item={item} />
            </TableCell>
            <TableCell label="وضعیت">
              <span className="flex flex-wrap items-center gap-2">
                <AttendanceStatusBadge item={item} />
                {/* No session was consumed (BUSINESS_RULES.md §7 *Cardio-only visit*). */}
                {item.isCardioOnly && <Badge variant="cardio">{cardioOnlyLabel}</Badge>}
                {/* Free on the birthday: no session either (§7 *Birthday visit*). */}
                {item.isBirthdayVisit && <Badge variant="birthday">{birthdayVisitLabel}</Badge>}
              </span>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * What this visit was charged for هوازی (BUSINESS_RULES.md §7 Gym services). Voided charges never
 * reach here — the API leaves them out — so an empty cell means the visit was not charged, not
 * that a charge was undone.
 *
 * A charge on a **closed** visit gets the full box, because debt outlives the thing that created
 * it (§5): the money is still owed and the desk must be able to take it here, since the visit
 * itself is over and has left the card above. An **open** visit shows only the amount, because
 * that same card is already showing its box a few lines up and two of them would be one too many.
 */
function CardioCell({ item }: { item: Attendance }) {
  const cardio = item.serviceCharges.find((charge) => charge.kind === "Cardio");

  if (cardio === undefined) {
    return emptyValue;
  }

  if (item.checkedOutAt === null) {
    return formatMoney(cardio.amount);
  }

  return (
    <ServiceChargeBox attendanceId={item.id} kind="Cardio" charge={cardio} visitIsOpen={false} />
  );
}

/**
 * Edged like the history's tags (13.2j): green for a visit still open, grey for one that is over.
 * A cancelled one keeps its plain outline, as in the history.
 */
function AttendanceStatusBadge({ item }: { item: Attendance }) {
  if (item.cancelledAt !== null) {
    return <Badge variant="destructive">لغو شده</Badge>;
  }
  if (item.autoClosedAt !== null) {
    return <Badge variant="secondary">بسته خودکار</Badge>;
  }
  if (item.checkedOutAt === null) {
    return <Badge variant="success">باز</Badge>;
  }

  return <Badge variant="secondary">بسته</Badge>;
}
