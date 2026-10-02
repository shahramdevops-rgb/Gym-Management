import { Badge } from "@/components/ui/badge";
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
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">عضو</th>
            <th className="py-2 text-start font-medium">ورود</th>
            <th className="py-2 text-start font-medium">خروج</th>
            <th className="py-2 text-start font-medium">کمد</th>
            <th className="py-2 text-start font-medium">ثبت ورود</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-b">
              <td className="py-2">
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
              </td>
              <td className="py-2">{formatDateTime(item.checkedInAt)}</td>
              <td className="py-2">
                {item.cancelledAt !== null ? (
                  emptyValue
                ) : (
                  <span className="flex items-center gap-2">
                    {formatDateTime(item.checkedOutAt)}
                    {item.autoClosedAt !== null && <Badge variant="secondary">خودکار</Badge>}
                  </span>
                )}
              </td>
              <td className="py-2">{placeOf(item)}</td>
              <td className="py-2">{item.checkedInByFullName ?? emptyValue}</td>
              <td className="py-2">
                <StatusBadge item={item} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
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
  if (item.checkedOutAt === null) {
    return <Badge variant="success">داخل باشگاه</Badge>;
  }

  return <Badge variant="secondary">خارج شده</Badge>;
}
