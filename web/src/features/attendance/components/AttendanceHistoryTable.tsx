import { Badge } from "@/components/ui/badge";
import { emptyValue, formatDateTime, toPersianDigits } from "@/lib/format";

import type { Attendance } from "../api";

/** A member's own visits, most recent first: cancelled and auto-closed ones included, marked. */
export function AttendanceHistoryTable({ items }: { items: Attendance[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">ورود</th>
            <th className="py-2 text-start font-medium">خروج</th>
            <th className="py-2 text-start font-medium">کمد</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-b">
              <td className="py-2">{formatDateTime(item.checkedInAt)}</td>
              <td className="py-2">{formatDateTime(item.checkedOutAt)}</td>
              <td className="py-2">
                {item.lockerNumber === null ? emptyValue : toPersianDigits(item.lockerNumber)}
              </td>
              <td className="py-2">
                <AttendanceStatusBadge item={item} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function AttendanceStatusBadge({ item }: { item: Attendance }) {
  if (item.cancelledAt !== null) {
    return <Badge variant="outline">لغو شده</Badge>;
  }
  if (item.autoClosedAt !== null) {
    return <Badge variant="secondary">بسته خودکار</Badge>;
  }
  if (item.checkedOutAt === null) {
    return <Badge variant="success">باز</Badge>;
  }

  return <Badge variant="secondary">بسته</Badge>;
}
