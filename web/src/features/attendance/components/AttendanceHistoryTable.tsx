import { Badge } from "@/components/ui/badge";
import { ServiceChargeBox } from "@/features/serviceCharges/components/ServiceChargeBox";
import { emptyValue, formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";

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
            <th className="py-2 text-start font-medium">هوازی</th>
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
                <CardioCell item={item} />
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
