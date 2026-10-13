import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cardioOnlyLabel } from "@/features/attendance/holder";
import { emptyValue, formatDateTime, toPersianDigits } from "@/lib/format";

import type { HistoryAttendance } from "../api";
import { WhoCell } from "./WhoCell";

/**
 * The gym's check-ins, newest first (BUSINESS_RULES.md §12 History): who came, which locker, who
 * let them in. A guest is listed under their name and marked «مهمان». A cancelled check-in stays
 * on the list, marked; a visit the nightly job closed says «خودکار» beside its check-out, because
 * no person did it.
 */
export function AttendanceLogTable({ items }: { items: HistoryAttendance[] }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>عضو</TableHead>
          <TableHead>ورود</TableHead>
          <TableHead>خروج</TableHead>
          <TableHead>کمد</TableHead>
          <TableHead>ثبت ورود</TableHead>
          <TableHead>وضعیت</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.id}>
            <TableCell className="tone-row-edge">
              <span className="flex flex-wrap items-center gap-2">
                <WhoCell
                  memberId={item.memberId}
                  memberFullName={item.memberFullName}
                  guestName={item.guestName}
                />
                {item.isCardioOnly && <Badge variant="cardio">{cardioOnlyLabel}</Badge>}
              </span>
            </TableCell>
            <TableCell label="ورود">{formatDateTime(item.checkedInAt)}</TableCell>
            <TableCell label="خروج">
              {item.cancelledAt !== null ? (
                emptyValue
              ) : (
                <span className="flex items-center gap-2">
                  {formatDateTime(item.checkedOutAt)}
                  {item.autoClosedAt !== null && <Badge variant="secondary">خودکار</Badge>}
                </span>
              )}
            </TableCell>
            <TableCell label="کمد">{placeOf(item)}</TableCell>
            <TableCell label="ثبت ورود">{item.checkedInByFullName ?? emptyValue}</TableCell>
            <TableCell label="وضعیت">
              <StatusBadge item={item} />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function placeOf(item: HistoryAttendance): string {
  if (item.lockerNumber !== null) {
    return toPersianDigits(item.lockerNumber);
  }

  return item.usesReservePlace ? "رزرو" : emptyValue;
}

function StatusBadge({ item }: { item: HistoryAttendance }) {
  if (item.cancelledAt !== null) {
    return <Badge variant="destructive">لغو شده</Badge>;
  }
  // Edged like a locker door, with no fill (asked by the developer): a faint frame and the thick
  // edge at the end, closing the row its start edge opens, as on the «داخل باشگاه» board's buttons.
  // Green for someone still inside, grey for a finished visit.
  if (item.checkedOutAt === null) {
    return <Badge variant="success">داخل باشگاه</Badge>;
  }

  return <Badge variant="secondary">خارج شده</Badge>;
}
