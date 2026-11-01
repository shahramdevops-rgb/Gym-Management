import { ArrowRight } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, Navigate } from "react-router";

import { JalaliDateField } from "@/components/FormField";
import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { errorMessage } from "@/lib/errors";
import { formatDate, toPersianDigits } from "@/lib/format";

import { useSiteMe } from "../auth/api";
import { SessionStudentsList } from "../bookings/SessionStudentsList";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import {
  useCheckResetTimetableDay,
  useCheckTimetableDay,
  useCheckWeekPattern,
  useResetTimetableDay,
  useSetTimetableDay,
  useSetWeekPattern,
  touchesNoOne,
  useTimetable,
  type DayOfWeek,
  type StudentToReschedule,
  type Timetable,
  type TimetableDay,
  type TimetableImpact,
} from "./api";
import { CopyToDays } from "./CopyToDays";
import {
  draftOf,
  inputsOf,
  sessionProblems,
  showFirstProblem,
  toDraft,
  type DraftSession,
} from "./drafts";
import { ImpactWarning, type HeldChange } from "./ImpactWarning";
import {
  addDays,
  dayNames,
  formatCapacity,
  formatDayLabel,
  formatSessionTimes,
  formatTaken,
  weekDays,
} from "./labels";
import { SessionsEditor } from "./SessionsEditor";
import { VipBadge } from "./VipBadge";
import { WeekNav } from "./WeekNav";

/**
 * The trainer's «برنامه سانس‌ها» (BUSINESS_RULES.md §15 *The trainer's sessions*, roadmap 15.6a):
 * the weekly pattern, repeated every week from the date it starts, and the timetable's twelve
 * weeks one week at a time, where a date shows its students (15.6b) and can be changed on its own,
 * closed, or given back to the pattern. A change that touches students warns first (§15 *Students
 * in sessions*). Only a trainer reaches it; anyone else is sent home.
 */
export function TimetablePage() {
  const me = useSiteMe();
  const timetable = useTimetable();

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.home} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <h1 className="text-xl font-bold">برنامه سانس‌ها</h1>
      </div>

      {timetable.data === undefined ? (
        timetable.isError ? (
          <Alert variant="destructive">{errorMessage(timetable.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <>
          <PatternCard timetable={timetable.data} />
          <WeeksCard timetable={timetable.data} />
        </>
      )}
    </SiteFrame>
  );
}

// ---- The weekly pattern ----

type WeekDrafts = Record<DayOfWeek, DraftSession[]>;

function draftsOf(timetable: Timetable): WeekDrafts {
  const sessions = timetable.pattern?.sessions ?? [];

  return Object.fromEntries(
    weekDays.map((day) => [day, sessions.filter((session) => session.day === day).map(draftOf)]),
  ) as WeekDrafts;
}

function PatternCard({ timetable }: { timetable: Timetable }) {
  const [editing, setEditing] = useState(false);
  const [toReschedule, setToReschedule] = useState<StudentToReschedule[]>([]);
  const pattern = timetable.pattern;

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          الگوی هفتگی
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {editing ? (
          <PatternEditor
            timetable={timetable}
            onDone={(saved = []) => {
              setEditing(false);
              setToReschedule(saved);
            }}
          />
        ) : (
          <>
            {toReschedule.length > 0 && (
              <RescheduleNotice students={toReschedule} onClose={() => setToReschedule([])} />
            )}
            {pattern === null ? (
              <p className="text-sm text-muted-foreground">
                هنوز الگویی نساخته‌اید. سانس‌های هر روز هفته را یک بار بسازید؛ هر هفته تکرار
                می‌شوند.
              </p>
            ) : (
              <>
                <p className="text-sm text-muted-foreground">
                  از {formatDate(pattern.startsOn)}، هر هفته تکرار می‌شود.
                </p>
                <ul aria-label="الگوی هفتگی" className="grid grid-cols-2 gap-2">
                  {weekDays.map((day) => {
                    const sessions = pattern.sessions.filter((session) => session.day === day);
                    return (
                      <li key={day} className="space-y-1 rounded-lg border p-2">
                        <p className="text-sm font-semibold">{dayNames[day]}</p>
                        {sessions.length === 0 ? (
                          <p className="text-xs text-muted-foreground">بدون سانس</p>
                        ) : (
                          sessions.map((session) => (
                            <p key={session.start + session.end} className="text-xs">
                              {formatSessionTimes(session)}
                              <span className="text-muted-foreground">
                                {" "}
                                · {formatCapacity(session.capacity)}
                              </span>
                              {session.isVip && (
                                <>
                                  {" "}
                                  <VipBadge />
                                </>
                              )}
                            </p>
                          ))
                        )}
                      </li>
                    );
                  })}
                </ul>
              </>
            )}
            <Button className="w-full" onClick={() => setEditing(true)}>
              {pattern === null ? "ساختن الگو" : "تغییر الگو"}
            </Button>
          </>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * After a pattern saved under the warning (roadmap 15.6b-4): the students whose fixed schedule lost
 * sessions to it, each a way to their «برنامهٔ ثابت». It stays until closed or the page is left.
 * The long editor left the page scrolled far below it, so it brings itself into view.
 */
function RescheduleNotice({
  students,
  onClose,
}: {
  students: StudentToReschedule[];
  onClose: () => void;
}) {
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    box.current?.scrollIntoView?.({ block: "start", behavior: "smooth" });
  }, []);

  return (
    <div
      ref={box}
      role="status"
      aria-label="برنامهٔ تازه لازم است"
      className="space-y-2 rounded-xl border border-destructive/30 bg-destructive/8 p-3 text-sm"
    >
      <p className="font-semibold">
        برنامهٔ ثابت این شاگردان سانس‌هایی دارد که دیگر نیست. برای هر کدام برنامهٔ تازه بچینید:
      </p>
      <div className="flex flex-wrap gap-1.5">
        {students.map((student) => (
          <Button key={student.memberId} asChild variant="outline" size="sm">
            <Link to={sitePaths.student(student.memberId)}>{student.fullName}</Link>
          </Button>
        ))}
      </div>
      <Button variant="ghost" size="sm" onClick={onClose}>
        بستن
      </Button>
    </div>
  );
}

/**
 * Every day of the week with its sessions, and the date the change applies from: tomorrow by
 * default, and the days before it keep what they had. A date changed on its own keeps its change.
 * Saved under the warning, it hands back the students whose fixed schedule lost sessions.
 */
function PatternEditor({
  timetable,
  onDone,
}: {
  timetable: Timetable;
  onDone: (toReschedule?: StudentToReschedule[]) => void;
}) {
  const [drafts, setDrafts] = useState(() => draftsOf(timetable));
  const tomorrow = addDays(timetable.today, 1);
  const [startsOn, setStartsOn] = useState(tomorrow);
  const check = useCheckWeekPattern();
  const save = useSetWeekPattern();
  const [held, setHeld] = useState<HeldChange | null>(null);
  // Shown from the first try to save on, then kept up to date as the trainer fixes them.
  const [showProblems, setShowProblems] = useState(false);
  const problems = new Map(weekDays.flatMap((day) => [...sessionProblems(drafts[day])]));
  const pending = check.isPending || save.isPending;
  // Nothing changes under a warning: the save it holds is the change it was given for.
  const locked = pending || held !== null;
  const failure = check.error ?? save.error;

  function copy(from: DayOfWeek, days: readonly DayOfWeek[]) {
    const copies = days.map((day) => [day, inputsOf(drafts[from]).map((input) => toDraft(input))]);
    setDrafts({ ...drafts, ...(Object.fromEntries(copies) as Partial<WeekDrafts>) });
  }

  function submit() {
    if (problems.size > 0) {
      setShowProblems(true);
      showFirstProblem();
      return;
    }
    const pattern = {
      startsOn,
      sessions: weekDays.flatMap((day) =>
        inputsOf(drafts[day]).map((session) => ({ day, ...session })),
      ),
    };
    const saveIt = (toReschedule: StudentToReschedule[]) =>
      save.mutate(pattern, { onSuccess: () => onDone(toReschedule) });
    check.mutate(pattern, {
      onSuccess: (impact) =>
        touchesNoOne(impact)
          ? saveIt([])
          : setHeld({ impact, save: () => saveIt(impact.toReschedule) }),
    });
  }

  return (
    <div className="space-y-4">
      {weekDays.map((day) => (
        <section key={day} aria-label={dayNames[day]} className="space-y-2">
          <h3 className="text-sm font-semibold">{dayNames[day]}</h3>
          <SessionsEditor
            label={dayNames[day]}
            sessions={drafts[day]}
            disabled={locked}
            problems={showProblems ? problems : undefined}
            onChange={(sessions) => setDrafts({ ...drafts, [day]: sessions })}
          />
          {drafts[day].length > 0 && (
            <CopyToDays from={day} disabled={locked} onCopy={(days) => copy(day, days)} />
          )}
        </section>
      ))}

      <div className="space-y-1 border-t pt-3">
        <JalaliDateField
          label="از تاریخ"
          value={startsOn}
          disabled={locked}
          onChange={(iso) => setStartsOn(iso === "" ? tomorrow : iso)}
        />
        <p className="text-xs text-muted-foreground">
          روزهای قبل از این تاریخ همان‌طور که بودند می‌مانند. روزهایی که جداگانه تغییر داده‌اید هم
          تغییرشان می‌ماند.
        </p>
      </div>

      {showProblems && <ProblemsAlert count={problems.size} />}
      {failure !== null && <Alert variant="destructive">{errorMessage(failure)}</Alert>}

      {held !== null ? (
        <ImpactWarning
          impact={held.impact}
          pending={pending}
          onSave={held.save}
          onCancel={() => setHeld(null)}
        />
      ) : (
        <div className="flex gap-2">
          <Button className="flex-1" disabled={pending} onClick={submit}>
            ذخیره الگو
          </Button>
          <Button variant="outline" disabled={pending} onClick={() => onDone()}>
            انصراف
          </Button>
        </div>
      )}
    </div>
  );
}

/** Beside the save buttons, which may be far below the red box: what to look for. */
function ProblemsAlert({ count }: { count: number }) {
  if (count === 0) {
    return null;
  }
  return (
    <Alert variant="destructive">
      {count === 1
        ? "یک سانس ایراد دارد. کادر قرمز را درست کنید."
        : `${toPersianDigits(count)} سانس ایراد دارند. کادرهای قرمز را درست کنید.`}
    </Alert>
  );
}

// ---- The twelve weeks ----

function WeeksCard({ timetable }: { timetable: Timetable }) {
  const weeks = Array.from({ length: Math.ceil(timetable.days.length / 7) }, (_, index) =>
    timetable.days.slice(index * 7, index * 7 + 7),
  );
  const [weekIndex, setWeekIndex] = useState(0);
  const [openDate, setOpenDate] = useState<string | null>(null);
  const week = weeks[Math.min(weekIndex, weeks.length - 1)] ?? [];
  const openDay = timetable.days.find((day) => day.date === openDate) ?? null;

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          روزهای پیش رو
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <WeekNav
          index={weekIndex}
          count={weeks.length}
          from={week[0]?.date}
          to={week.at(-1)?.date}
          onChange={setWeekIndex}
        />

        <ul aria-label="روزهای هفته" className="space-y-2">
          {week.map((day) => (
            <li key={day.date}>
              <button
                type="button"
                className="w-full space-y-1 rounded-lg border p-2 text-start hover:bg-muted/50"
                onClick={() => setOpenDate(day.date)}
              >
                <span className="flex items-center gap-2 text-sm font-semibold">
                  {formatDayLabel(day.date)}
                  {day.date === timetable.today && <Badge variant="secondary">امروز</Badge>}
                  {day.changedByHand && <Badge variant="outline">تغییر جداگانه</Badge>}
                </span>
                <DaySessions day={day} />
              </button>
            </li>
          ))}
        </ul>
      </CardContent>

      {openDay !== null && (
        <DayDialog
          key={openDay.date}
          day={openDay}
          isToday={openDay.date === timetable.today}
          onClose={() => setOpenDate(null)}
        />
      )}
    </Card>
  );
}

function DaySessions({ day }: { day: TimetableDay }) {
  if (day.sessions.length === 0) {
    return (
      <span className="block text-xs text-muted-foreground">
        {day.changedByHand ? "تعطیل" : "بدون سانس"}
      </span>
    );
  }

  return (
    <span className="flex flex-wrap gap-1">
      {day.sessions.map((session) => (
        <span
          key={session.start + session.end}
          className="inline-flex items-center gap-1 rounded-md bg-muted px-1.5 py-0.5 text-xs"
        >
          {formatSessionTimes(session)} · {formatTaken(session.students, session.capacity)}
          {session.isVip && <VipBadge />}
        </span>
      ))}
    </span>
  );
}

/**
 * One date: first «شاگردان سانس», each session's students (15.6b); then, behind «تغییر سانس‌های
 * این روز», its sessions changed, the day closed, or given back to the pattern. On today a
 * session that has started stays as it was; the API says so if it is touched.
 */
function DayDialog({
  day,
  isToday,
  onClose,
}: {
  day: TimetableDay;
  isToday: boolean;
  onClose: () => void;
}) {
  const [editing, setEditing] = useState(false);

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{formatDayLabel(day.date)}</DialogTitle>
          <DialogDescription>
            {editing
              ? `این تغییر فقط برای همین روز است؛ هفته‌های دیگر همان الگو را دارند.${isToday ? " سانسی که شروع شده تغییر نمی‌کند." : ""}`
              : "شاگردان هر سانس در این روز."}
          </DialogDescription>
        </DialogHeader>

        {editing ? (
          <DayEditor day={day} onDone={onClose} />
        ) : (
          <>
            <SessionStudentsList date={day.date} />
            <Button variant="outline" onClick={() => setEditing(true)}>
              تغییر سانس‌های این روز
            </Button>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

/** A date's sessions changed on their own; a change that touches students warns first. */
function DayEditor({ day, onDone }: { day: TimetableDay; onDone: () => void }) {
  const [drafts, setDrafts] = useState(() => day.sessions.map(draftOf));
  const checkDay = useCheckTimetableDay();
  const checkReset = useCheckResetTimetableDay();
  const save = useSetTimetableDay();
  const reset = useResetTimetableDay();
  const [held, setHeld] = useState<HeldChange | null>(null);
  const [showProblems, setShowProblems] = useState(false);
  const problems = sessionProblems(drafts);
  const pending = checkDay.isPending || checkReset.isPending || save.isPending || reset.isPending;
  const failure = checkDay.error ?? checkReset.error ?? save.error ?? reset.error;

  function hold(saveIt: () => void) {
    return {
      onSuccess: (impact: TimetableImpact) =>
        touchesNoOne(impact) ? saveIt() : setHeld({ impact, save: saveIt }),
    };
  }

  function saveDay(sessions: DraftSession[]) {
    // Closing the day sends no session, so the boxes' problems do not stand in its way.
    if (sessions.length > 0 && problems.size > 0) {
      setShowProblems(true);
      showFirstProblem();
      return;
    }
    const input = { date: day.date, sessions: inputsOf(sessions) };
    checkDay.mutate(
      input,
      hold(() => save.mutate(input, { onSuccess: onDone })),
    );
  }

  function resetDay() {
    checkReset.mutate(
      day.date,
      hold(() => reset.mutate(day.date, { onSuccess: onDone })),
    );
  }

  return (
    <>
      <SessionsEditor
        label={formatDayLabel(day.date)}
        sessions={drafts}
        disabled={pending || held !== null}
        problems={showProblems ? problems : undefined}
        onChange={setDrafts}
      />

      {showProblems && <ProblemsAlert count={problems.size} />}
      {failure !== null && <Alert variant="destructive">{errorMessage(failure)}</Alert>}

      {held !== null ? (
        <ImpactWarning
          impact={held.impact}
          pending={pending}
          onSave={held.save}
          onCancel={() => setHeld(null)}
        />
      ) : (
        <div className="flex flex-wrap gap-2">
          <Button disabled={pending} onClick={() => saveDay(drafts)}>
            ذخیره
          </Button>
          <Button variant="outline" disabled={pending} onClick={() => saveDay([])}>
            تعطیل کردن این روز
          </Button>
          {day.changedByHand && (
            <Button variant="outline" disabled={pending} onClick={resetDay}>
              برگرداندن به الگو
            </Button>
          )}
        </div>
      )}
    </>
  );
}
