import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Button } from "@/components/ui/button";
import { ServiceChargeBox } from "@/features/serviceCharges/components/ServiceChargeBox";
import { formatDateTime, toPersianDigits } from "@/lib/format";

import type { CurrentlyInside } from "../api";

interface CurrentlyInsideTableProps {
  rows: CurrentlyInside[];
  /** The one attendance a check-out or cancel is in flight for, or null. */
  busyAttendanceId: string | null;
  onCheckOut: (attendanceId: string) => void;
  onCancel: (attendanceId: string) => void;
}

/**
 * The front desk board: who is inside right now, which locker, since when, and what they are
 * being charged for هوازی (BUSINESS_RULES.md §7 Gym services). Every row here is an open visit
 * by definition, so the charge box is always in its editable state.
 */
export function CurrentlyInsideTable({
  rows,
  busyAttendanceId,
  onCheckOut,
  onCancel,
}: CurrentlyInsideTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">عضو</th>
            <th className="py-2 text-start font-medium">کمد</th>
            <th className="py-2 text-start font-medium">ساعت ورود</th>
            <th className="py-2 text-start font-medium">هوازی</th>
            <th className="py-2 text-start font-medium">
              <span className="sr-only">عملیات</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const busy = busyAttendanceId === row.attendanceId;

            return (
              <tr key={row.attendanceId} className="border-b">
                <td className="py-2">
                  <Link
                    to={paths.member(row.memberId)}
                    className="font-medium underline-offset-4 hover:underline"
                  >
                    {row.memberFullName}
                  </Link>
                </td>
                <td className="py-2">
                  {row.lockerNumber === null ? "—" : toPersianDigits(row.lockerNumber)}
                </td>
                <td className="py-2">{formatDateTime(row.checkedInAt)}</td>
                <td className="py-2">
                  <ServiceChargeBox
                    attendanceId={row.attendanceId}
                    kind="Cardio"
                    charge={row.serviceCharges.find((charge) => charge.kind === "Cardio")}
                    visitIsOpen
                    disabled={busy}
                  />
                </td>
                <td className="py-2">
                  <div className="flex justify-end gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={busy}
                      onClick={() => onCancel(row.attendanceId)}
                    >
                      لغو ورود
                    </Button>
                    <Button
                      size="sm"
                      variant="secondary"
                      disabled={busy}
                      onClick={() => onCheckOut(row.attendanceId)}
                    >
                      ثبت خروج
                    </Button>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
