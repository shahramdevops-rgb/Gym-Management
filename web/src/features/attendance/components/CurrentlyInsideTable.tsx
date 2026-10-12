import { Link } from "react-router";

import { paths } from "@/app/paths";
import { SessionsBar } from "@/components/SessionsBar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { stateEdge } from "@/lib/edges";
import { formatDate, formatDateTime, gymToday, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { CurrentlyInside } from "../api";
import { cardioOnlyLabel, guestLabel } from "../holder";
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
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>عضو</TableHead>
          <TableHead>کمد</TableHead>
          <TableHead>ساعت ورود</TableHead>
          <TableHead>جلسات</TableHead>
          <TableHead>انقضا</TableHead>
          <TableHead>
            <span className="sr-only">عملیات</span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((row) => {
          if (row.memberId === null) {
            return (
              <GuestRow
                key={row.attendanceId}
                row={row}
                onCheckOut={onCheckOut}
                onCancel={onCancel}
              />
            );
          }

          const daysLeft = daysUntil(row.subscriptionEndDate ?? today, today);

          // A cardio-only visit may name no plan (BUSINESS_RULES.md §7 *Cardio-only visit*):
          // nothing to count and nothing running out.
          const hasPlan = row.subscriptionId !== null;

          // A single visit is spent by design and expires tonight, so both marks would fire on
          // every such row and mean nothing. What the desk needs to see is that it was a single
          // visit (BUSINESS_RULES.md §4, §7).
          const expiringSoon = hasPlan && !row.isSingleSession && daysLeft <= expiringDaysThreshold;

          return (
            <TableRow key={row.attendanceId}>
              {/* Edged at its start in the page's tone, like a locker door (`tone-row-edge`). */}
              <TableCell className="tone-row-edge">
                <Link
                  to={paths.member(row.memberId)}
                  className="font-medium underline-offset-4 hover:underline"
                >
                  {row.memberFullName}
                </Link>
                {/* No session came from the plan beside it (BUSINESS_RULES.md §7 *Cardio-only visit*). */}
                {row.isCardioOnly && (
                  <Badge variant="outline" className="ms-2 border-cardio">
                    {cardioOnlyLabel}
                  </Badge>
                )}
              </TableCell>
              <TableCell>
                {/* A reserve place has no number to show (BUSINESS_RULES.md §6, §7). */}
                {row.lockerNumber !== null
                  ? toPersianDigits(row.lockerNumber)
                  : row.usesReservePlace
                    ? "رزرو"
                    : "—"}
              </TableCell>
              <TableCell>{formatDateTime(row.checkedInAt)}</TableCell>
              <TableCell>
                {!hasPlan ? (
                  <span className="text-muted-foreground">—</span>
                ) : row.isSingleSession ? (
                  // No bar: "۱ از ۱" on every single-visit row is a denominator with nothing to
                  // say.
                  <span className="whitespace-nowrap text-muted-foreground">تک‌جلسه‌ای</span>
                ) : (
                  <SessionsBar
                    total={row.totalSessions ?? 0}
                    used={row.usedSessions ?? 0}
                    remaining={row.remainingSessions ?? 0}
                    lowThreshold={lowSessionsThreshold}
                  />
                )}
              </TableCell>
              <TableCell
                className={cn(
                  "py-2 whitespace-nowrap",
                  expiringSoon && "font-medium text-warning-ink",
                )}
              >
                {row.subscriptionEndDate !== null && formatDate(row.subscriptionEndDate)}
                {expiringSoon && (
                  <span className="ms-1 text-xs">
                    {daysLeft <= 0 ? "(امروز)" : `(${toPersianDigits(daysLeft)} روز)`}
                  </span>
                )}
              </TableCell>
              <TableCell>
                <RowActions row={row} onCheckOut={onCheckOut} onCancel={onCancel} />
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

/**
 * A guest on the board (BUSINESS_RULES.md §7 *Guest visit*): the name with no profile to link to,
 * «مهمان» where the sessions go, and nothing that could mark the row as needing attention: a guest
 * has no plan to run out.
 */
function GuestRow({
  row,
  onCheckOut,
  onCancel,
}: {
  row: CurrentlyInside;
} & Pick<CurrentlyInsideTableProps, "onCheckOut" | "onCancel">) {
  return (
    <TableRow>
      <TableCell className="font-medium tone-row-edge">{row.guestName}</TableCell>
      <TableCell>
        {row.lockerNumber !== null
          ? toPersianDigits(row.lockerNumber)
          : row.usesReservePlace
            ? "رزرو"
            : "—"}
      </TableCell>
      <TableCell>{formatDateTime(row.checkedInAt)}</TableCell>
      <TableCell>
        <span className="whitespace-nowrap text-muted-foreground">{guestLabel}</span>
      </TableCell>
      <TableCell className="text-muted-foreground">—</TableCell>
      <TableCell>
        <RowActions row={row} onCheckOut={onCheckOut} onCancel={onCancel} />
      </TableCell>
    </TableRow>
  );
}

/**
 * The row's two buttons, frameless with a thick edge like a locker door's (asked by the
 * developer): red for «لغو ورود», which undoes the visit, green for «ثبت خروج», which ends it.
 * The thick edge sits at the end, closing the row its start edge opens.
 */
function RowActions({
  row,
  onCheckOut,
  onCancel,
}: {
  row: CurrentlyInside;
} & Pick<CurrentlyInsideTableProps, "onCheckOut" | "onCancel">) {
  return (
    <div className="flex justify-end gap-2">
      <Button
        size="sm"
        variant="ghost"
        className={cn(stateEdge.destructive, "hover:bg-destructive/10 hover:text-destructive")}
        onClick={() => onCancel(row)}
      >
        لغو ورود
      </Button>
      <Button
        size="sm"
        variant="ghost"
        className={cn(stateEdge.success, "hover:bg-success/10 hover:text-success-ink")}
        onClick={() => onCheckOut(row)}
      >
        ثبت خروج
      </Button>
    </div>
  );
}
