import { ChevronLeft, ChevronRight } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import { formatDayLabel, formatSessionTimes } from "../timetable/labels";
import { VipBadge } from "../timetable/VipBadge";
import type { SessionPresence, StudentAttendance } from "./api";
import {
  formatMonth,
  formatVisit,
  otherDayLabel,
  otherDayVariant,
  presenceLabels,
  presenceVariants,
  sameMonth,
  shiftMonth,
  type JalaliMonth,
  type PresenceVariant,
} from "./labels";
import { PresenceBadge } from "./PresenceBadge";
import { boxTones, toneOf } from "./tones";

type AttendanceItem = StudentAttendance["sessions"][number];
type AttendanceTotals = StudentAttendance["totals"];
type OtherDayVisit = StudentAttendance["otherDays"][number];

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

interface CountBox {
  label: string;
  count: number | string;
  variant: PresenceVariant;
}

/** A row of the month: a session with its state, or a visit on a day with no session (15.7p). */
type MonthRow =
  | { kind: "session"; date: string; time: string; item: AttendanceItem }
  | { kind: "otherDay"; date: string; time: string; visit: OtherDayVisit };

/** By date, then time: a session by its start, a visit by its check-in. */
function monthRows(sessions: AttendanceItem[], otherDays: OtherDayVisit[]): MonthRow[] {
  const rows: MonthRow[] = [
    ...sessions.map((item) => ({
      kind: "session" as const,
      date: item.date,
      time: item.start,
      item,
    })),
    ...otherDays.map((visit) => ({
      kind: "otherDay" as const,
      date: visit.date,
      time: visit.visit.checkedInAt,
      visit,
    })),
  ];

  const order = (row: MonthRow) => `${row.date} ${row.time}`;

  return rows.sort((a, b) => order(a).localeCompare(order(b)));
}

/**
 * One month of attendance (BUSINESS_RULES.md §15 *Attendance for the trainer*): the totals in small
 * boxes, each in its state's colour (15.7p, asked by the developer), then each session with its
 * state; a session with a trainer other than the one now names them. Shared by the trainer's
 * «حضور» of a student (15.7b) and the member's «حضور من» (15.7j), alike since 15.7p.
 *
 * Since 15.7p (the developer, 1405/08/29) a day with a session is told by its session alone: early,
 * on time, late, «خارج از سانس» (came that day at another hour) or absent (did not come at all). A
 * visit on a day with no session to come to is a row of its own among them, «روز دیگر» (purple).
 * Eight boxes, four a row: the four a student who came is in, then absent, «روز دیگر» and what was
 * no one's fault.
 */
export function AttendanceMonth({
  totals,
  sessions,
  otherDays,
  empty,
}: {
  totals: AttendanceTotals;
  sessions: AttendanceItem[];
  otherDays: OtherDayVisit[];
  /** What an empty month says. */
  empty: string;
}) {
  const box = (state: SessionPresence, count: number | string): CountBox => ({
    label: presenceLabels[state],
    count,
    variant: presenceVariants[state],
  });

  const boxes: CountBox[] = [
    box("Early", totals.early),
    box("OnTime", totals.onTime),
    box("Late", totals.late),
    box("OutsideSession", totals.outsideSession),
    box("Absent", totals.absent),
    { label: otherDayLabel, count: totals.otherDayVisits, variant: otherDayVariant },
    box("Leave", totals.leave),
    box("Cancelled", totals.cancelled),
  ];

  const rows = monthRows(sessions, otherDays);

  return (
    <>
      <ul aria-label="جمع ماه" className="grid grid-cols-4 gap-1.5">
        {boxes.map((item) => {
          const tone = boxTones[toneOf(item.variant)];

          return (
            <li key={item.label} className={cn("rounded-lg px-1 py-2 text-center", tone.box)}>
              <p className={cn("text-lg font-bold", tone.count)}>{toPersianDigits(item.count)}</p>
              <p className="text-xs text-muted-foreground">{item.label}</p>
            </li>
          );
        })}
      </ul>

      {rows.length === 0 ? (
        <p className="text-sm text-muted-foreground">{empty}</p>
      ) : (
        <ul aria-label="حضور ماه" className="divide-y rounded-xl border bg-card px-3 shadow-raised">
          {rows.map((row) =>
            row.kind === "session" ? (
              <SessionRow key={`s-${row.date}-${row.item.start}-${row.item.end}`} item={row.item} />
            ) : (
              <OtherDayRow key={`v-${row.date}-${row.time}`} visit={row.visit} />
            ),
          )}
        </ul>
      )}
    </>
  );
}

function SessionRow({ item }: { item: AttendanceItem }) {
  return (
    <li className="flex items-center justify-between gap-2 py-2.5">
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
  );
}

/** A visit on a day with no session: its day, «ورود … · خروج …» and «روز دیگر». */
function OtherDayRow({ visit }: { visit: OtherDayVisit }) {
  return (
    <li className="flex items-center justify-between gap-2 py-2.5">
      <div className="space-y-0.5">
        <p className="font-semibold">{formatDayLabel(visit.date)}</p>
        <p className="text-sm text-muted-foreground">{formatVisit(visit.visit)}</p>
      </div>
      <Badge variant={otherDayVariant}>{otherDayLabel}</Badge>
    </li>
  );
}
