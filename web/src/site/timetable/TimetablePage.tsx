import { ArrowRight, ChevronLeft, ChevronRight } from "lucide-react";
import { useState } from "react";
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
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import {
  useResetTimetableDay,
  useSetTimetableDay,
  useSetWeekPattern,
  useTimetable,
  type DayOfWeek,
  type Timetable,
  type TimetableDay,
} from "./api";
import {
  dayNames,
  formatCapacity,
  formatDayLabel,
  formatDayMonth,
  formatSessionTimes,
  weekDays,
} from "./labels";
import { draftOf, inputsOf, type DraftSession } from "./drafts";
import { SessionsEditor } from "./SessionsEditor";

/**
 * The trainer's «برنامه سانس‌ها» (BUSINESS_RULES.md §15 *The trainer's sessions*, roadmap 15.6a):
 * the weekly pattern, repeated every week from the date it starts, and the timetable's twelve
 * weeks one week at a time, where a date can be changed on its own, closed, or given back to the
 * pattern. Only a trainer reaches it; anyone else is sent home.
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

/** The day after the timetable's today: the earliest a new pattern can start. */
function dayAfter(iso: string): string {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}

function PatternCard({ timetable }: { timetable: Timetable }) {
  const [editing, setEditing] = useState(false);
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
          <PatternEditor timetable={timetable} onDone={() => setEditing(false)} />
        ) : (
          <>
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
 * Every day of the week with its sessions, and the date the change applies from: tomorrow by
 * default, and the days before it keep what they had. A date changed on its own keeps its change.
 */
function PatternEditor({ timetable, onDone }: { timetable: Timetable; onDone: () => void }) {
  const [drafts, setDrafts] = useState(() => draftsOf(timetable));
  const tomorrow = dayAfter(timetable.today);
  const [startsOn, setStartsOn] = useState(tomorrow);
  const save = useSetWeekPattern();

  function submit() {
    const sessions = weekDays.flatMap((day) =>
      inputsOf(drafts[day]).map((session) => ({ day, ...session })),
    );
    save.mutate({ startsOn, sessions }, { onSuccess: onDone });
  }

  return (
    <div className="space-y-4">
      {weekDays.map((day) => (
        <section key={day} aria-label={dayNames[day]} className="space-y-2">
          <h3 className="text-sm font-semibold">{dayNames[day]}</h3>
          <SessionsEditor
            label={dayNames[day]}
            sessions={drafts[day]}
            disabled={save.isPending}
            onChange={(sessions) => setDrafts({ ...drafts, [day]: sessions })}
          />
        </section>
      ))}

      <div className="space-y-1 border-t pt-3">
        <JalaliDateField
          label="از تاریخ"
          value={startsOn}
          disabled={save.isPending}
          onChange={(iso) => setStartsOn(iso === "" ? tomorrow : iso)}
        />
        <p className="text-xs text-muted-foreground">
          روزهای قبل از این تاریخ همان‌طور که بودند می‌مانند. روزهایی که جداگانه تغییر داده‌اید هم
          تغییرشان می‌ماند.
        </p>
      </div>

      {save.isError && <Alert variant="destructive">{errorMessage(save.error)}</Alert>}

      <div className="flex gap-2">
        <Button className="flex-1" disabled={save.isPending} onClick={submit}>
          ذخیره الگو
        </Button>
        <Button variant="outline" disabled={save.isPending} onClick={onDone}>
          انصراف
        </Button>
      </div>
    </div>
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
        <div className="flex items-center justify-between gap-2">
          <Button
            variant="ghost"
            size="sm"
            aria-label="هفتهٔ قبل"
            disabled={weekIndex === 0}
            onClick={() => setWeekIndex(weekIndex - 1)}
          >
            <ChevronRight aria-hidden />
          </Button>
          <p className="text-sm font-semibold" aria-live="polite">
            هفتهٔ {toPersianDigits(weekIndex + 1)} از {toPersianDigits(weeks.length)}
            {week.length > 0 && (
              <span className="block text-center text-xs font-normal text-muted-foreground">
                {formatDayMonth(week[0]!.date)} تا {formatDayMonth(week.at(-1)!.date)}
              </span>
            )}
          </p>
          <Button
            variant="ghost"
            size="sm"
            aria-label="هفتهٔ بعد"
            disabled={weekIndex >= weeks.length - 1}
            onClick={() => setWeekIndex(weekIndex + 1)}
          >
            <ChevronLeft aria-hidden />
          </Button>
        </div>

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
          className="rounded-md bg-muted px-1.5 py-0.5 text-xs"
        >
          {formatSessionTimes(session)} · {formatCapacity(session.capacity)}
        </span>
      ))}
    </span>
  );
}

/**
 * One date on its own: its sessions changed, the day closed, or given back to the pattern. On
 * today a session that has started stays as it was; the API says so if it is touched.
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
  const [drafts, setDrafts] = useState(() => day.sessions.map(draftOf));
  const save = useSetTimetableDay();
  const reset = useResetTimetableDay();
  const pending = save.isPending || reset.isPending;
  const failure = save.error ?? reset.error;

  function saveDay(sessions: DraftSession[]) {
    save.mutate(
      {
        date: day.date,
        sessions: inputsOf(sessions),
      },
      { onSuccess: onClose },
    );
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{formatDayLabel(day.date)}</DialogTitle>
          <DialogDescription>
            این تغییر فقط برای همین روز است؛ هفته‌های دیگر همان الگو را دارند.
            {isToday && " سانسی که شروع شده تغییر نمی‌کند."}
          </DialogDescription>
        </DialogHeader>

        <SessionsEditor
          label={formatDayLabel(day.date)}
          sessions={drafts}
          disabled={pending}
          onChange={setDrafts}
        />

        {failure !== null && <Alert variant="destructive">{errorMessage(failure)}</Alert>}

        <div className="flex flex-wrap gap-2">
          <Button disabled={pending} onClick={() => saveDay(drafts)}>
            ذخیره
          </Button>
          <Button variant="outline" disabled={pending} onClick={() => saveDay([])}>
            تعطیل کردن این روز
          </Button>
          {day.changedByHand && (
            <Button
              variant="outline"
              disabled={pending}
              onClick={() => reset.mutate(day.date, { onSuccess: onClose })}
            >
              برگرداندن به الگو
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
