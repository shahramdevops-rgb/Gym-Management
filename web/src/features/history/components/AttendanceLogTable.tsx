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
import { stateEdge } from "@/lib/edges";
import { emptyValue, formatDateTime, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

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
                {item.isCardioOnly && (
                  <Badge variant="outline" className="border-cardio">
                    {cardioOnlyLabel}
                  </Badge>
                )}
              </span>
            </TableCell>
            <TableCell>{formatDateTime(item.checkedInAt)}</TableCell>
            <TableCell>
              {item.cancelledAt !== null ? (
                emptyValue
              ) : (
                <span className="flex items-center gap-2">
                  {formatDateTime(item.checkedOutAt)}
                  {item.autoClosedAt !== null && <Badge variant="secondary">خودکار</Badge>}
                </span>
              )}
            </TableCell>
            <TableCell>{placeOf(item)}</TableCell>
            <TableCell>{item.checkedInByFullName ?? emptyValue}</TableCell>
            <TableCell>
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
    return <Badge variant="outline">لغو شده</Badge>;
  }
  // Edged like a locker door, with no fill (asked by the developer): a faint frame and the thick
  // edge at the end, closing the row its start edge opens, as on the «داخل باشگاه» board's buttons.
  // Green for someone still inside, grey for a finished visit.
  if (item.checkedOutAt === null) {
    return (
      <Badge variant="ghost" className={cn(stateEdge.success, "font-semibold")}>
        داخل باشگاه
      </Badge>
    );
  }

  return (
    <Badge variant="ghost" className={stateEdge.muted}>
      خارج شده
    </Badge>
  );
}
