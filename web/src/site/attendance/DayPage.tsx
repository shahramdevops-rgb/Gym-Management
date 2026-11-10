import { ArrowRight, ChevronLeft, ChevronRight } from "lucide-react";
import { Link, Navigate, useSearchParams } from "react-router";

import { JalaliCalendarField } from "@/components/FormField";
import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import { formatTime, gymToday } from "@/lib/format";

import { useSiteMe } from "../auth/api";
import { Person } from "../components/Person";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { formatDayLabel, formatSessionTimes } from "../timetable/labels";
import { SessionNotes } from "../timetable/SessionNotes";
import { VipBadge } from "../timetable/VipBadge";
import { useTrainerDay, useTrainerToday, type DaySession, type TrainerDay } from "./api";
import { inFilter, parseDayFilter, type DayFilter } from "./dayFilter";
import { DayTotalsBoxes } from "./DayTotalsBoxes";
import { formatVisit, shiftDay } from "./labels";
import { PresenceBadge } from "./PresenceBadge";

/**
 * «حضور امروز» and any earlier day (BUSINESS_RULES.md §15 *Attendance for the trainer* and *More
 * for the trainer*, roadmap 15.7b and 15.7d): the day's counts, its sessions each in its own box
 * with who is in it and whether they checked in and out at the desk, then «آمدند، بدون سانس».
 * «روز قبل» / «روز بعد» and the date box move it, with no limit back and not past today; the day
 * is in the address (`?date=`), so a reload keeps it. Only today's asks again every minute and says
 * when it last did. A count box shows only its students, pressed again everything (15.7f); the
 * filter is in the address too (`?show=`), so the home's «امروز» opens the page on it and moving
 * to another day keeps it. Only a trainer reaches it.
 */
export function DayPage() {
  const me = useSiteMe();
  const today = gymToday();
  const [params, setParams] = useSearchParams();
  const picked = params.get("date");
  const date = picked === null || picked >= today ? today : picked;
  const isToday = date === today;
  const filter = parseDayFilter(params.get("show"));

  const todays = useTrainerToday({ refresh: true, enabled: isToday });
  const earlier = useTrainerDay(date, { enabled: !isToday });
  const day = isToday ? todays : earlier;

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  function show(iso: string, next: DayFilter | null) {
    setParams(
      {
        ...(iso >= today ? {} : { date: iso }),
        ...(next === null ? {} : { show: next }),
      },
      { replace: true },
    );
  }

  function goTo(iso: string) {
    show(iso, filter);
  }

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.home} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <div>
          <h1 className="text-xl font-bold">{isToday ? "حضور امروز" : "حضور"}</h1>
          {!isToday && <p className="text-sm text-muted-foreground">{formatDayLabel(date)}</p>}
        </div>
      </div>

      <div className="flex items-end gap-2">
        <Button variant="outline" size="sm" onClick={() => goTo(shiftDay(date, -1))}>
          <ChevronRight aria-hidden />
          روز قبل
        </Button>
        <div className="min-w-0 flex-1">
          <JalaliCalendarField
            label="تاریخ"
            value={date}
            onChange={(iso) => {
              if (iso !== "") {
                goTo(iso);
              }
            }}
          />
        </div>
        <Button
          variant="outline"
          size="sm"
          disabled={isToday}
          onClick={() => goTo(shiftDay(date, 1))}
        >
          روز بعد
          <ChevronLeft aria-hidden />
        </Button>
      </div>

      {day.data === undefined ? (
        day.isError ? (
          <Alert variant="destructive">{errorMessage(day.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <>
          {isToday && day.isError && (
            <Alert variant="destructive">
              به‌روز نشد؛ آنچه می‌بینید از ساعت{" "}
              {formatTime(new Date(day.dataUpdatedAt).toISOString())} است.
            </Alert>
          )}
          <DayView
            day={day.data}
            isToday={isToday}
            filter={filter}
            onFilter={(next) => show(date, next === filter ? null : next)}
          />
          {isToday && (
            <p className="text-center text-xs text-muted-foreground">
              هر دقیقه به‌روز می‌شود · آخرین بار ساعت{" "}
              {formatTime(new Date(day.dataUpdatedAt).toISOString())}
            </p>
          )}
        </>
      )}
    </SiteFrame>
  );
}

function DayView({
  day,
  isToday,
  filter,
  onFilter,
}: {
  day: TrainerDay;
  isToday: boolean;
  filter: DayFilter | null;
  onFilter: (filter: DayFilter) => void;
}) {
  // A filter keeps only its students, and only the sessions that have one of them.
  const sessions =
    filter === null
      ? day.sessions
      : day.sessions
          .map((session) => ({
            ...session,
            students: session.students.filter((student) => inFilter(student.state, filter)),
          }))
          .filter((session) => session.students.length > 0);

  return (
    <>
      {day.sessions.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          {isToday ? "امروز سانسی ندارید." : "در این روز سانسی نداشتید."}
        </p>
      ) : (
        <>
          <DayTotalsBoxes
            totals={day.totals}
            label={isToday ? "جمع امروز" : "جمع روز"}
            selected={filter}
            onSelect={onFilter}
          />
          {sessions.length === 0 ? (
            <p className="text-sm text-muted-foreground">کسی در این دسته نیست.</p>
          ) : (
            <ul aria-label="سانس‌های روز" className="space-y-3">
              {sessions.map((session) => (
                <SessionBox
                  key={`${session.start}-${session.end}-${session.removed}`}
                  session={session}
                />
              ))}
            </ul>
          )}
        </>
      )}

      {filter === null && day.outside.length > 0 && (
        <section aria-labelledby="outside-heading" className="space-y-2">
          <h2 id="outside-heading" className="font-semibold">
            آمدند، بدون سانس
          </h2>
          <ul
            aria-label="آمدند، بدون سانس"
            className="divide-y rounded-xl border bg-card px-3 shadow-raised"
          >
            {day.outside.map((student) => (
              <li key={student.memberId} className="py-2.5">
                <Person
                  fullName={student.fullName}
                  phoneNumber={student.phoneNumber}
                  detail={student.visits.map(formatVisit).join("، ")}
                />
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
}

function SessionBox({ session }: { session: DaySession }) {
  const times = formatSessionTimes(session);

  return (
    <li aria-label={times} className="space-y-2 rounded-xl border bg-card p-3 shadow-raised">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-semibold">{times}</span>
        {session.isVip && <VipBadge />}
        {session.removed ? (
          <Badge variant="destructive">لغو شد</Badge>
        ) : session.ended ? (
          <Badge variant="outline">تمام شد</Badge>
        ) : (
          session.started && <Badge variant="outline">در حال برگزاری</Badge>
        )}
      </div>
      <SessionNotes note={session.note} dateNote={session.dateNote} />
      {session.students.length === 0 ? (
        <p className="text-sm text-muted-foreground">شاگردی در این سانس نیست.</p>
      ) : (
        <ul aria-label={`شاگردان ${times}`} className="divide-y">
          {session.students.map((student) => (
            <li
              key={student.memberId}
              className="flex items-center justify-between gap-2 py-2 first:pt-0 last:pb-0"
            >
              <Person fullName={student.fullName} phoneNumber={student.phoneNumber} />
              <PresenceBadge
                state={student.state}
                checkedInAt={student.checkedInAt}
                checkedOutAt={student.checkedOutAt}
                autoCheckedOut={student.autoCheckedOut}
                minutesEarly={student.minutesEarly}
                minutesLate={student.minutesLate}
                hadLeave={student.hadLeave}
              />
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}
