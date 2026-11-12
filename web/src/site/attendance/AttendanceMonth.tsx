import { ChevronLeft, ChevronRight } from "lucide-react";

import { Button } from "@/components/ui/button";
import { toPersianDigits } from "@/lib/format";

import { formatDayLabel, formatSessionTimes } from "../timetable/labels";
import { VipBadge } from "../timetable/VipBadge";
import type { StudentAttendance } from "./api";
import {
  formatMonth,
  formatVisit,
  presenceLabels,
  sameMonth,
  shiftMonth,
  type JalaliMonth,
} from "./labels";
import { PresenceBadge } from "./PresenceBadge";

type AttendanceItem = StudentAttendance["sessions"][number];
type AttendanceTotals = StudentAttendance["totals"];
type OutsideVisit = StudentAttendance["outside"][number];

/** «ماه قبل» / «ماه بعد» around the month's name; never past this month. */
export function MonthNavigator({
  month,
  thisMonth,
  onChange,
}: {
  month: JalaliMonth;
  thisMonth: JalaliMonth;
  onChange: (month: JalaliMonth) => void;
}) {
  return (
    <div className="flex items-center justify-between gap-2">
      <Button variant="outline" size="sm" onClick={() => onChange(shiftMonth(month, -1))}>
        <ChevronRight aria-hidden />
        ماه قبل
      </Button>
      <p className="font-semibold" aria-live="polite">
        {formatMonth(month)}
      </p>
      <Button
        variant="outline"
        size="sm"
        disabled={sameMonth(month, thisMonth)}
        onClick={() => onChange(shiftMonth(month, 1))}
      >
        ماه بعد
        <ChevronLeft aria-hidden />
      </Button>
    </div>
  );
}

/**
 * One month of attendance (BUSINESS_RULES.md §15 *Attendance for the trainer*): the totals in small
 * boxes, then each session with its state; a session with a trainer other than the one now names
 * them. Shared by the trainer's «حضور» of a student (15.7b) and the member's «حضور من» (15.7j);
 * only the trainer's has «خارج از سانس» (15.7d).
 */
export function AttendanceMonth({
  totals,
  sessions,
  outside = [],
  empty,
}: {
  totals: AttendanceTotals;
  sessions: AttendanceItem[];
  outside?: OutsideVisit[];
  /** What an empty month says. */
  empty: string;
}) {
  const boxes = [
    { label: presenceLabels.OnTime, count: totals.onTime },
    { label: presenceLabels.Late, count: totals.late },
    { label: "بعد از سانس", count: totals.cameAfter },
    { label: presenceLabels.Absent, count: totals.absent },
    { label: presenceLabels.Leave, count: totals.leave },
    { label: presenceLabels.Cancelled, count: totals.cancelled },
  ];

  return (
    <>
      <ul aria-label="جمع ماه" className="grid grid-cols-3 gap-1.5">
        {boxes.map((box) => (
          <li key={box.label} className="rounded-lg bg-muted px-1 py-2 text-center">
            <p className="text-lg font-bold">{toPersianDigits(box.count)}</p>
            <p className="text-xs text-muted-foreground">{box.label}</p>
          </li>
        ))}
      </ul>

      {sessions.length === 0 ? (
        <p className="text-sm text-muted-foreground">{empty}</p>
      ) : (
        <ul
          aria-label="سانس‌های ماه"
          className="divide-y rounded-xl border bg-card px-3 shadow-raised"
        >
          {sessions.map((item) => (
            <li
              key={`${item.date}-${item.start}-${item.end}`}
              className="flex items-center justify-between gap-2 py-2.5"
            >
              <div className="space-y-0.5">
                <p className="font-semibold">{formatDayLabel(item.date)}</p>
                <p className="flex flex-wrap items-center gap-x-2 text-sm text-muted-foreground">
                  {formatSessionTimes(item)}
                  {item.isVip && <VipBadge />}
                  {item.trainerName !== null && <span>با {item.trainerName}</span>}
                </p>
              </div>
              <PresenceBadge
                state={item.state}
                checkedInAt={item.checkedInAt}
                checkedOutAt={item.checkedOutAt}
                autoCheckedOut={item.autoCheckedOut}
                minutesEarly={item.minutesEarly}
                minutesLate={item.minutesLate}
                hadLeave={item.hadLeave}
              />
            </li>
          ))}
        </ul>
      )}

      {outside.length > 0 && (
        <section aria-labelledby="outside-heading" className="space-y-2">
          <h2 id="outside-heading" className="font-semibold">
            خارج از سانس
          </h2>
          <ul
            aria-label="خارج از سانس"
            className="divide-y rounded-xl border bg-card px-3 shadow-raised"
          >
            {outside.map((item) => (
              <li
                key={`${item.date}-${item.visit.checkedInAt}`}
                className="flex flex-wrap items-center justify-between gap-x-2 py-2.5"
              >
                <span className="font-semibold">{formatDayLabel(item.date)}</span>
                <span className="text-sm text-muted-foreground">{formatVisit(item.visit)}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
}
