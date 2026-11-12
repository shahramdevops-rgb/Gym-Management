import { ArrowRight } from "lucide-react";
import { useState } from "react";
import { Link, Navigate, useNavigate, useParams } from "react-router";

import { JalaliDateField } from "@/components/FormField";
import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate } from "@/lib/format";

import { useSiteMe } from "../auth/api";
import { useEndStudent } from "../coaching/api";
import {
  nextPeriodStart,
  periodDefaultDays,
  periodEnd,
  periodLine,
  periodMinDays,
  periodMinDaysLeft,
  periodState,
} from "../coaching/period";
import { PeriodDaysForm } from "../coaching/PeriodDaysForm";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { useTimetable, type Timetable } from "../timetable/api";
import {
  addDays,
  dayNames,
  formatDayLabel,
  formatSessionTimes,
  formatSlot,
  toMinutes,
  weekDays,
} from "../timetable/labels";
import {
  useCheckStudentSchedule,
  useEndStudentSchedule,
  useRenewCoachingPeriod,
  useSetStudentSchedule,
  useStudentSchedule,
  type SessionDate,
  type SlotInput,
  type StudentSchedule,
} from "./api";
import { SessionNotes } from "../timetable/SessionNotes";
import { PlaceBadges } from "./PlaceBadges";
import { PlacesByWeek } from "./PlacesByWeek";

/**
 * A student from before the coaching period (15.7h) has none until the trainer types the days left:
 * their program's last date is still 8 weeks on by default, from the first, as before.
 */
const daysWithoutPeriod = 8 * 7 - 1;

/**
 * The trainer's «برنامهٔ ثابت» of one student (BUSINESS_RULES.md §15 *Who puts a member in a
 * session* and *Students in sessions*, roadmap 15.6b): the weekly sessions they come to, from a
 * first date to a last, and every date that puts them in. Saved whole or not at all: a date with
 * no room keeps everything as it was and is named. Only a trainer reaches it. Ending the coaching
 * («پایان مربی‌گری») sits at its foot, away from the home, so a finger cannot hit it by mistake (15.7g).
 * The coaching period comes first (15.7h): the program ends on its last day or before, and a period
 * that is over takes no program until «تمدید».
 */
export function StudentSchedulePage() {
  const { memberId = "" } = useParams();
  const me = useSiteMe();
  const schedule = useStudentSchedule(memberId);

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.students} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <div>
          <h1 className="text-xl font-bold">برنامهٔ ثابت</h1>
          {schedule.data !== undefined && (
            <p className="text-sm text-muted-foreground">{schedule.data.fullName}</p>
          )}
        </div>
      </div>

      {schedule.data === undefined ? (
        schedule.isError ? (
          <Alert variant="destructive">{errorMessage(schedule.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <>
          <PeriodCard student={schedule.data} />
          <ScheduleCard student={schedule.data} />
          <PlacesCard student={schedule.data} />
          <EndCoachingCard student={schedule.data} />
        </>
      )}
    </SiteFrame>
  );
}

function ScheduleCard({ student }: { student: StudentSchedule }) {
  const [editing, setEditing] = useState(false);
  const end = useEndStudentSchedule(student.memberId);
  const schedule = student.schedule;
  const periodOver = periodState(student.periodEndsOn, student.today).kind === "over";

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          سانس‌های هفتگی
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {editing ? (
          <ScheduleEditor student={student} onDone={() => setEditing(false)} />
        ) : (
          <>
            {schedule === null ? (
              <p className="text-sm text-muted-foreground">
                هنوز برنامهٔ ثابتی ندارد. روزها و ساعت‌هایی را که باید بیاید انتخاب کنید؛ تا آخرین
                روز برنامه، هر هفته در همان سانس‌هاست.
              </p>
            ) : (
              <>
                <p className="text-sm text-muted-foreground">
                  از {formatDate(schedule.startsOn)} تا {formatDate(schedule.endsOn)}
                </p>
                <ul aria-label="سانس‌های هفتگی" className="flex flex-wrap gap-1">
                  {schedule.slots.map((slot) => (
                    <li
                      key={slot.day + slot.start + slot.end}
                      className="rounded-md bg-muted px-2 py-1 text-sm"
                    >
                      {formatSlot(slot)}
                    </li>
                  ))}
                </ul>
              </>
            )}
            {end.isError && <Alert variant="destructive">{errorMessage(end.error)}</Alert>}
            {periodOver && (
              <p className="text-sm text-muted-foreground">
                دورهٔ مربی‌گری تمام شده است؛ برای برنامهٔ تازه اول دوره را تمدید کنید.
              </p>
            )}
            <div className="flex gap-2">
              <Button className="flex-1" disabled={periodOver} onClick={() => setEditing(true)}>
                {schedule === null ? "ساختن برنامه" : "تغییر برنامه"}
              </Button>
              {schedule !== null && (
                <ConfirmDialog
                  label="پایان برنامه"
                  question={`برنامهٔ ثابت ${student.fullName} از همین حالا تمام می‌شود؛ فقط سانس‌های امروز که شروع شده‌اند می‌مانند. ادامه می‌دهید؟`}
                  pending={end.isPending}
                  onConfirm={(close) => end.mutate(undefined, { onSettled: close })}
                />
              )}
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

const slotKey = (slot: SlotInput) => `${slot.day} ${slot.start} ${slot.end}`;

/**
 * Whether each of the weekly pattern's sessions is VIP, by `slotKey`: a schedule's session outside
 * them is gone.
 */
function patternSessions(timetable: Timetable): Map<string, boolean> {
  return new Map(
    (timetable.pattern?.sessions ?? []).map((session) => [
      slotKey({ day: session.day, start: toMinutes(session.start), end: toMinutes(session.end) }),
      session.isVip,
    ]),
  );
}

/** The weekly sessions to pick from: the pattern's, and the schedule's own if the pattern has since lost one. */
function choicesOf(timetable: Timetable, student: StudentSchedule): SlotInput[] {
  const all = [...(timetable.pattern?.sessions ?? []), ...(student.schedule?.slots ?? [])].map(
    (session) => ({
      day: session.day,
      start: toMinutes(session.start),
      end: toMinutes(session.end),
    }),
  );
  const unique = new Map(all.map((slot) => [slotKey(slot), slot]));

  return [...unique.values()].sort(
    (a, b) =>
      weekDays.indexOf(a.day) - weekDays.indexOf(b.day) ||
      a.start.localeCompare(b.start) ||
      a.end.localeCompare(b.end),
  );
}

const earlier = (a: string, b: string) => (a < b ? a : b);

/**
 * The weekly sessions picked from the trainer's, and the first and last dates. A change applies from
 * now: today's sessions that have started stay as they were (§15).
 */
function ScheduleEditor({ student, onDone }: { student: StudentSchedule; onDone: () => void }) {
  const timetable = useTimetable();

  if (timetable.data === undefined) {
    return timetable.isError ? (
      <Alert variant="destructive">{errorMessage(timetable.error)}</Alert>
    ) : (
      <LoadingState className="py-2" />
    );
  }

  return <ScheduleForm student={student} timetable={timetable.data} onDone={onDone} />;
}

function ScheduleForm({
  student,
  timetable,
  onDone,
}: {
  student: StudentSchedule;
  timetable: Timetable;
  onDone: () => void;
}) {
  const current = student.schedule;
  const choices = choicesOf(timetable, student);
  // The student's sessions the pattern no longer has (it changed under them): shown, off, and not
  // to be picked, since the API refuses a schedule with a session the trainer does not hold.
  const inPattern = patternSessions(timetable);
  const isGone = (slot: SlotInput) => !inPattern.has(slotKey(slot));
  const firstDay = student.today;
  // The period's last day, or 8 weeks on for a student from before it; never past the timetable.
  const defaultEnd = (startsOn: string) =>
    earlier(student.periodEndsOn ?? addDays(startsOn, daysWithoutPeriod), student.lastDay);

  const [picked, setPicked] = useState(
    () =>
      new Set(
        (current?.slots ?? [])
          .map((slot) =>
            slotKey({ day: slot.day, start: toMinutes(slot.start), end: toMinutes(slot.end) }),
          )
          .filter((key) => inPattern.has(key)),
      ),
  );
  const [startsOn, setStartsOn] = useState(firstDay);
  const [endsOn, setEndsOn] = useState(
    current !== null && current.endsOn >= firstDay ? current.endsOn : defaultEnd(firstDay),
  );
  const [full, setFull] = useState<SessionDate[]>([]);
  const check = useCheckStudentSchedule(student.memberId);
  const save = useSetStudentSchedule(student.memberId);
  const pending = check.isPending || save.isPending;
  const failure = check.error ?? save.error;

  function toggle(slot: SlotInput) {
    const next = new Set(picked);
    if (!next.delete(slotKey(slot))) {
      next.add(slotKey(slot));
    }
    setPicked(next);
    setFull([]);
  }

  function submit() {
    const schedule = {
      startsOn,
      endsOn,
      slots: choices.filter((slot) => picked.has(slotKey(slot))),
    };
    check.mutate(schedule, {
      onSuccess: (result) =>
        result.full.length > 0
          ? setFull(result.full)
          : save.mutate(schedule, { onSuccess: onDone }),
    });
  }

  if (choices.length === 0) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-muted-foreground">
          هنوز الگوی هفتگی ندارید. اول در «تمام سانس‌های من» سانس‌های هفته را بسازید.
        </p>
        <div className="flex gap-2">
          <Button asChild className="flex-1">
            <Link to={sitePaths.timetable}>تمام سانس‌های من</Link>
          </Button>
          <Button variant="outline" onClick={onDone}>
            انصراف
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <ul aria-label="سانس‌های الگو" className="space-y-2">
        {weekDays
          .filter((day) => choices.some((slot) => slot.day === day))
          .map((day) => (
            <li key={day} className="space-y-1">
              <p className="text-sm font-semibold">{dayNames[day]}</p>
              <div className="flex flex-wrap gap-1">
                {choices
                  .filter((slot) => slot.day === day)
                  .map((slot) => {
                    const on = picked.has(slotKey(slot));
                    const gone = isGone(slot);
                    const vip = inPattern.get(slotKey(slot)) === true;
                    return (
                      <Button
                        key={slotKey(slot)}
                        size="sm"
                        variant={on ? "default" : "outline"}
                        aria-pressed={on}
                        aria-label={
                          gone
                            ? `${formatSlot(slot)}، دیگر در الگوی شما نیست`
                            : `${formatSlot(slot)}${vip ? "، VIP" : ""}`
                        }
                        disabled={pending || gone}
                        className={gone ? "border-dashed" : undefined}
                        onClick={() => toggle(slot)}
                      >
                        <span className={gone ? "line-through" : undefined}>
                          {formatSessionTimes(slot)}
                        </span>
                        {vip && <span className="text-xs font-semibold">VIP</span>}
                        {gone && <span className="text-xs">دیگر نیست</span>}
                      </Button>
                    );
                  })}
              </div>
            </li>
          ))}
      </ul>

      <div className="grid gap-3 border-t pt-3 sm:grid-cols-2">
        <JalaliDateField
          label="از تاریخ"
          value={startsOn}
          disabled={pending}
          onChange={(iso) => {
            const next = iso === "" ? firstDay : iso;
            setStartsOn(next);
            setEndsOn(defaultEnd(next));
            setFull([]);
          }}
        />
        <JalaliDateField
          label="تا تاریخ"
          value={endsOn}
          disabled={pending}
          onChange={(iso) => {
            setEndsOn(iso === "" ? defaultEnd(startsOn) : iso);
            setFull([]);
          }}
        />
      </div>
      <p className="text-xs text-muted-foreground">
        {current !== null && current.startsOn <= student.today
          ? "تغییر از همین حالا اعمال می‌شود؛ سانس‌های امروز که شروع شده‌اند همان می‌مانند. "
          : ""}
        روزی که آن سانس را ندارد (تعطیل یا تغییر جداگانه) رد می‌شود.
      </p>

      {full.length > 0 && (
        <Alert variant="destructive">
          <div className="space-y-1">
            <p>این سانس‌ها جای خالی ندارند و برنامه ذخیره نشد:</p>
            <ul aria-label="سانس‌های پر">
              {full.map((session) => (
                <li key={session.date + session.start + session.end}>
                  {formatDayLabel(session.date)}، {formatSessionTimes(session)}
                </li>
              ))}
            </ul>
            <p>ظرفیت آن سانس‌ها را بیشتر کنید یا سانس دیگری انتخاب کنید.</p>
          </div>
        </Alert>
      )}
      {failure !== null && <Alert variant="destructive">{errorMessage(failure)}</Alert>}

      <div className="flex gap-2">
        <Button className="flex-1" disabled={pending} onClick={submit}>
          {pending ? "در حال ثبت…" : "ذخیره برنامه"}
        </Button>
        <Button variant="outline" disabled={pending} onClick={onDone}>
          انصراف
        </Button>
      </div>
    </div>
  );
}

/**
 * The coaching period (BUSINESS_RULES.md §15 *The coaching period*, roadmap 15.7h): its last day
 * and the days left, and «تمدید», which adds the days typed after the last day so none is lost. A
 * student from before it has none: the trainer types the days left from today, as few as one, and
 * is warned when that cuts the fixed schedule.
 */
function PeriodCard({ student }: { student: StudentSchedule }) {
  const [renewing, setRenewing] = useState(false);
  const renew = useRenewCoachingPeriod(student.memberId);
  const state = periodState(student.periodEndsOn, student.today);
  const unset = state.kind === "unset";
  const scheduleEndsOn = student.schedule?.endsOn ?? null;

  const cutNote = (days: number) => {
    const endsOn = periodEnd(student.today, days);
    return scheduleEndsOn !== null && scheduleEndsOn > endsOn
      ? `برنامهٔ ثابت او تا ${formatDate(scheduleEndsOn)} است و با این دوره ${formatDate(endsOn)} تمام می‌شود.`
      : null;
  };

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          دورهٔ مربی‌گری
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className={unset ? "text-sm" : "text-sm text-muted-foreground"}>
          {unset
            ? "این شاگرد پیش از دوره‌ها شاگرد شما شده است. چند روز از مربی‌گری او مانده؟"
            : periodLine(state)}
        </p>
        {renew.isError && <Alert variant="destructive">{errorMessage(renew.error)}</Alert>}
        {renewing || unset ? (
          <PeriodDaysForm
            label={unset ? "روزهای باقی‌مانده" : "تمدید"}
            startsOn={nextPeriodStart(student.periodEndsOn, student.today)}
            defaultDays={unset ? null : periodDefaultDays}
            minDays={unset ? periodMinDaysLeft : periodMinDays}
            submitLabel={unset ? "ثبت" : "تمدید"}
            pending={renew.isPending}
            note={unset ? cutNote : undefined}
            onSubmit={(days) => renew.mutate(days, { onSuccess: () => setRenewing(false) })}
            onCancel={unset ? undefined : () => setRenewing(false)}
          />
        ) : (
          <Button variant="secondary" className="w-full" onClick={() => setRenewing(true)}>
            تمدید
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * «پایان مربی‌گری» for this student (BUSINESS_RULES.md §15 *A member and their trainer*: either side
 * may end the link, asking first; it ends that day). Moved here from «شاگردان من» on the home
 * (15.7g). Once ended the student is no longer the trainer's, so the page goes back to the home.
 */
function EndCoachingCard({ student }: { student: StudentSchedule }) {
  const end = useEndStudent();
  const navigate = useNavigate();

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          پایان مربی‌گری
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-sm text-muted-foreground">
          اگر {student.fullName} دیگر شاگرد شما نیست، مربی‌گری را تمام کنید؛ از امروز در «شاگردان
          من» نیست.
        </p>
        {end.isError && <Alert variant="destructive">{errorMessage(end.error)}</Alert>}
        <ConfirmDialog
          wide
          label="پایان مربی‌گری"
          question={`مربی‌گری شما برای ${student.fullName} از امروز تمام می‌شود. ادامه می‌دهید؟`}
          pending={end.isPending}
          onConfirm={(close) =>
            end.mutate(student.memberId, {
              onSettled: close,
              onSuccess: () => void navigate(sitePaths.students, { replace: true }),
            })
          }
        />
      </CardContent>
    </Card>
  );
}

function PlacesCard({ student }: { student: StudentSchedule }) {
  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          سانس‌های پیش رو
        </CardTitle>
      </CardHeader>
      <CardContent>
        <PlacesByWeek today={student.today} places={student.sessions} empty="سانسی در پیش ندارد.">
          {(week) => (
            <ul aria-label="سانس‌های پیش رو" className="grid grid-cols-2 gap-2">
              {week.map((place) => (
                <li
                  key={place.date + place.start + place.end}
                  className="space-y-1 rounded-lg border p-2"
                >
                  <p className="text-sm font-semibold">{formatDayLabel(place.date)}</p>
                  <p className="text-xs">{formatSessionTimes(place)}</p>
                  <p className="flex flex-wrap gap-1">
                    <PlaceBadges place={place} />
                  </p>
                  <SessionNotes note={place.note} dateNote={place.dateNote} compact />
                </li>
              ))}
            </ul>
          )}
        </PlacesByWeek>
      </CardContent>
    </Card>
  );
}
