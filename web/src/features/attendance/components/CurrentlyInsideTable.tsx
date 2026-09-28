import { Link } from "react-router";

import { paths } from "@/app/paths";
import { SessionsBar } from "@/components/SessionsBar";
import { Button } from "@/components/ui/button";
import { formatDate, formatDateTime, gymToday, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { CurrentlyInside } from "../api";
import { daysUntil, expiringDaysThreshold, lowSessionsThreshold } from "../renewal";

interface CurrentlyInsideTableProps {
  rows: CurrentlyInside[];
  /** Opens the check-out box; the row carries the member and the locker it needs. */
  onCheckOut: (row: CurrentlyInside) => void;
  /** Opens the "cancel this check-in?" box. */
  onCancel: (row: CurrentlyInside) => void;
}

/**
 * The front desk board: who is inside right now, which locker, since when, and how much of their
 * subscription is left. Every row is exactly one line: the desk reads this board while somebody
 * is standing in front of them.
 *
 * Nothing is bought from here. هوازی and the cafe are rung up from the member's locker, or the
 * cafe from its till, so a visit's purchases have one place to be looked at (BUSINESS_RULES.md §7).
 *
 * No status column: check-in refuses a subscription that is not usable today, so every row would
 * read "فعال". What the desk cannot see otherwise is how close the subscription is to running
 * out, which is marked instead, and only when it is true.
 */
export function CurrentlyInsideTable({ rows, onCheckOut, onCancel }: CurrentlyInsideTableProps) {
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
            <th className="py-2 text-start font-medium">
              <span className="sr-only">عملیات</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const daysLeft = daysUntil(row.subscriptionEndDate, today);

            // A single visit is spent by design and expires tonight, so both marks would fire on
            // every such row and mean nothing. What the desk needs to see is that it was a single
            // visit (BUSINESS_RULES.md §4, §7).
            const expiringSoon = !row.isSingleSession && daysLeft <= expiringDaysThreshold;

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
                  {/* A reserve place has no number to show (BUSINESS_RULES.md §6, §7). */}
                  {row.lockerNumber !== null
                    ? toPersianDigits(row.lockerNumber)
                    : row.usesReservePlace
                      ? "رزرو"
                      : "—"}
                </td>
                <td className="py-2">{formatDateTime(row.checkedInAt)}</td>
                <td className="py-2">
                  {row.isSingleSession ? (
                    // No bar: "۱ از ۱" on every single-visit row is a denominator with nothing to
                    // say.
                    <span className="whitespace-nowrap text-muted-foreground">تک‌جلسه‌ای</span>
                  ) : (
                    <SessionsBar
                      total={row.totalSessions}
                      used={row.usedSessions}
                      remaining={row.remainingSessions}
                      lowThreshold={lowSessionsThreshold}
                    />
                  )}
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
                  <div className="flex justify-end gap-2">
                    <Button size="sm" variant="outline" onClick={() => onCancel(row)}>
                      لغو ورود
                    </Button>
                    <Button size="sm" variant="secondary" onClick={() => onCheckOut(row)}>
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
