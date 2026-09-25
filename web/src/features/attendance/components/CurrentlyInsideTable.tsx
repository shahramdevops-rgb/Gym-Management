import { Link } from "react-router";

import { paths } from "@/app/paths";
import { SessionsBar } from "@/components/SessionsBar";
import { Button } from "@/components/ui/button";
import { ServiceChargeBox } from "@/features/serviceCharges/components/ServiceChargeBox";
import { formatDate, formatDateTime, gymToday, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { CurrentlyInside } from "../api";

/**
 * When the desk should mention renewing (BUSINESS_RULES.md §7, the "currently inside" board).
 * The same numbers Phase 10's SMS reminders are to be configured with, so the desk and the
 * member's text message do not disagree about what "running out" means.
 */
export const lowSessionsThreshold = 3;
export const expiringDaysThreshold = 5;

interface CurrentlyInsideTableProps {
  rows: CurrentlyInside[];
  /** The one attendance a check-out or cancel is in flight for, or null. */
  busyAttendanceId: string | null;
  onCheckOut: (attendanceId: string) => void;
  onCancel: (attendanceId: string) => void;
}

/**
 * Whole days from the gym's today until a subscription's last day; negative once it has passed.
 * Both dates are read at noon, so a daylight-saving shift cannot turn a day into 23 or 25 hours
 * and round the wrong way.
 */
function daysUntil(endDate: string, today: string): number {
  const end = new Date(`${endDate}T12:00:00Z`).getTime();
  const start = new Date(`${today}T12:00:00Z`).getTime();

  return Math.round((end - start) / 86_400_000);
}

/**
 * The front desk board: who is inside right now, which locker, since when, how much of their
 * subscription is left, and what they are being charged for هوازی.
 *
 * Every row is exactly one line, and stays one line whatever state it is in — the charge forms
 * open in a dialog rather than inside the cell (task 6.5.2). Rows that grow are not a cosmetic
 * problem here: the desk reads this board while somebody is standing in front of them, and the
 * cafe will want the same slot.
 *
 * No status column: check-in refuses a subscription that is not usable today, so every row would
 * read "فعال". What the desk cannot see otherwise is how close the subscription is to running
 * out, which is marked instead, and only when it is true.
 */
export function CurrentlyInsideTable({
  rows,
  busyAttendanceId,
  onCheckOut,
  onCancel,
}: CurrentlyInsideTableProps) {
  // The gym's day, not the browser's: the same definition IGymCalendar.Today() uses on the API.
  const today = gymToday();

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">عضو</th>
            <th className="py-2 text-start font-medium">کمد</th>
            <th className="py-2 text-start font-medium">ساعت ورود</th>
            <th className="py-2 text-start font-medium">جلسات</th>
            <th className="py-2 text-start font-medium">انقضا</th>
            <th className="py-2 text-start font-medium">هوازی</th>
            <th className="py-2 text-start font-medium">
              <span className="sr-only">عملیات</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const busy = busyAttendanceId === row.attendanceId;
            const daysLeft = daysUntil(row.subscriptionEndDate, today);
            const expiringSoon = daysLeft <= expiringDaysThreshold;

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
                  <SessionsBar
                    total={row.totalSessions}
                    used={row.usedSessions}
                    remaining={row.remainingSessions}
                    lowThreshold={lowSessionsThreshold}
                  />
                </td>
                <td
                  className={cn(
                    "py-2 whitespace-nowrap",
                    expiringSoon && "font-medium text-warning",
                  )}
                >
                  {formatDate(row.subscriptionEndDate)}
                  {expiringSoon && (
                    <span className="ms-1 text-xs">
                      {daysLeft <= 0 ? "(امروز)" : `(${toPersianDigits(daysLeft)} روز)`}
                    </span>
                  )}
                </td>
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
