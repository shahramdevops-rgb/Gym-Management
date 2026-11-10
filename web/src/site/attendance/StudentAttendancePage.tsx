import { ArrowRight, ChevronLeft, ChevronRight } from "lucide-react";
import { useState } from "react";
import { Link, Navigate, useParams } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import { gymToday, toPersianDigits } from "@/lib/format";

import { useSiteMe } from "../auth/api";
import { useStudents } from "../coaching/api";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { formatDayLabel, formatSessionTimes } from "../timetable/labels";
import { VipBadge } from "../timetable/VipBadge";
import { useStudentAttendance, type StudentAttendance } from "./api";
import {
  formatMonth,
  formatVisit,
  monthOf,
  monthRange,
  presenceLabels,
  sameMonth,
  shiftMonth,
} from "./labels";
import { PresenceBadge } from "./PresenceBadge";

/**
 * A student's «حضور» (BUSINESS_RULES.md §15 *Attendance for the trainer*, roadmap 15.7b): their
 * sessions of one Jalali month up to today, each with its state, and the month's totals in small
 * boxes. A session with an earlier trainer shows that trainer's name. The month moves back as far
 * as the trainer likes and forward up to this one. Under them, «خارج از سانس» (15.7d): the visits
 * that counted for none of the student's sessions. Only a trainer reaches it.
 */
export function StudentAttendancePage() {
  const { memberId = "" } = useParams();
  const me = useSiteMe();
  const students = useStudents();
  const thisMonth = monthOf(gymToday());
  const [month, setMonth] = useState(thisMonth);
  const { from, to } = monthRange(month);
  const attendance = useStudentAttendance(memberId, from, to);

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  const fullName = students.data?.students.find(
    (student) => student.memberId === memberId,
  )?.fullName;

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.home} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <div>
          <h1 className="text-xl font-bold">حضور</h1>
          {fullName !== undefined && <p className="text-sm text-muted-foreground">{fullName}</p>}
        </div>
      </div>

      <div className="flex items-center justify-between gap-2">
        <Button variant="outline" size="sm" onClick={() => setMonth(shiftMonth(month, -1))}>
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
          onClick={() => setMonth(shiftMonth(month, 1))}
        >
          ماه بعد
          <ChevronLeft aria-hidden />
        </Button>
      </div>

      {attendance.data === undefined ? (
        attendance.isError ? (
          <Alert variant="destructive">{errorMessage(attendance.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <MonthView attendance={attendance.data} />
      )}
    </SiteFrame>
  );
}

function MonthView({ attendance }: { attendance: StudentAttendance }) {
  const { totals, sessions } = attendance;
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
        <p className="text-sm text-muted-foreground">در این ماه سانسی نداشته است.</p>
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
              />
            </li>
          ))}
        </ul>
      )}

      {attendance.outside.length > 0 && (
        <section aria-labelledby="outside-heading" className="space-y-2">
          <h2 id="outside-heading" className="font-semibold">
            خارج از سانس
          </h2>
          <ul
            aria-label="خارج از سانس"
            className="divide-y rounded-xl border bg-card px-3 shadow-raised"
          >
            {attendance.outside.map((item) => (
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
