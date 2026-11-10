import { ArrowRight } from "lucide-react";
import { useState } from "react";
import { Link, Navigate } from "react-router";

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
import { ConfirmDialog } from "../components/ConfirmDialog";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { formatDayLabel, formatSessionTimes, formatSlot } from "../timetable/labels";
import {
  useCancelMyLeave,
  useLeaveMySession,
  useMoveMySession,
  useMySessions,
  useReturnToMySession,
  type MySessions,
  type Opening,
  type Place,
} from "./api";
import { PlaceBadges } from "./PlaceBadges";
import { PlacesByWeek } from "./PlacesByWeek";

/**
 * The member's «سانس‌های من» (BUSINESS_RULES.md §15 *Who puts a member in a session* and *Students
 * in sessions*, roadmap 15.6b): the fixed schedule their trainer made, and every date it puts them
 * in. Until a session starts, the member moves it to another of the same trainer's sessions with
 * room, up to the schedule's last date. By the day before, they ask the trainer for leave from it
 * («درخواست مرخصی», asked first, 15.7i): it waits for the trainer («لغو درخواست» takes it back), and
 * a declined one says so. A date they have leave from stays listed, faded, «مرخصی», with «می‌آیم» to
 * take it back while it has room. They never pick the schedule itself. Only a member reaches it.
 */
export function MySessionsPage() {
  const me = useSiteMe();
  const mine = useMySessions();

  if (me.data !== undefined && me.data.member === null) {
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
        <h1 className="text-xl font-bold">سانس‌های من</h1>
        {/* «حضور من» (15.7j): also without a trainer now, for the sessions with an earlier one. */}
        <Button asChild variant="secondary" size="sm" className="ms-auto">
          <Link to={sitePaths.myAttendance}>حضور من</Link>
        </Button>
      </div>

      {mine.data === undefined ? (
        mine.isError ? (
          <Alert variant="destructive">{errorMessage(mine.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : mine.data.trainerName === null ? (
        <Card accent={false} className="gap-3 shadow-raised">
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">
              مربی ندارید؛ سانس‌ها را مربی‌تان برایتان می‌چیند.
            </p>
            <Button asChild variant="secondary" className="w-full">
              <Link to={sitePaths.trainers}>انتخاب مربی</Link>
            </Button>
          </CardContent>
        </Card>
      ) : (
        <>
          <ScheduleCard mine={mine.data} />
          <DatesCard mine={mine.data} />
        </>
      )}
    </SiteFrame>
  );
}

function ScheduleCard({ mine }: { mine: MySessions }) {
  const schedule = mine.schedule;

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          برنامهٔ ثابت
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        <p className="text-sm">مربی: {mine.trainerName}</p>
        {schedule === null ? (
          <p className="text-sm text-muted-foreground">
            مربی‌تان هنوز برنامهٔ سانسی برایتان نچیده است.
          </p>
        ) : (
          <>
            <p className="text-sm text-muted-foreground">
              از {formatDate(schedule.startsOn)} تا {formatDate(schedule.endsOn)}، هر هفته:
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
      </CardContent>
    </Card>
  );
}

function DatesCard({ mine }: { mine: MySessions }) {
  const leave = useLeaveMySession();
  const cancelLeave = useCancelMyLeave();
  const comeBack = useReturnToMySession();
  const [moving, setMoving] = useState<Place | null>(null);

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          سانس‌های پیش رو
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {leave.isError && <Alert variant="destructive">{errorMessage(leave.error)}</Alert>}
        {cancelLeave.isError && (
          <Alert variant="destructive">{errorMessage(cancelLeave.error)}</Alert>
        )}
        {comeBack.isError && <Alert variant="destructive">{errorMessage(comeBack.error)}</Alert>}
        <PlacesByWeek today={mine.today} places={mine.sessions} empty="سانسی در پیش ندارید.">
          {(week) => (
            <ul aria-label="سانس‌های پیش رو" className="divide-y">
              {week.map((place) => {
                const label = `${formatDayLabel(place.date)}، ${formatSessionTimes(place)}`;
                return (
                  <li
                    key={place.date + place.start + place.end}
                    aria-label={label}
                    className="flex flex-wrap items-center justify-between gap-2 py-3 first:pt-0 last:pb-0"
                  >
                    <div className={place.left ? "space-y-0.5 opacity-60" : "space-y-0.5"}>
                      <p className="text-sm font-semibold">{formatDayLabel(place.date)}</p>
                      <p className="flex flex-wrap items-center gap-1 text-sm">
                        {formatSessionTimes(place)}
                        <PlaceBadges place={place} />
                        {place.started && <Badge variant="secondary">شروع شده</Badge>}
                        {place.left && <Badge variant="outline">مرخصی</Badge>}
                        {place.leave === "Pending" && (
                          <Badge variant="warning" className="max-w-full whitespace-normal">
                            در انتظار تأیید مربی
                          </Badge>
                        )}
                        {place.leave === "Declined" && (
                          <Badge variant="destructive" className="max-w-full whitespace-normal">
                            مرخصی رد شد
                          </Badge>
                        )}
                      </p>
                    </div>
                    {place.left ? (
                      // Taking a leave back needs no question: it is asked for again the same way.
                      <Button
                        variant="secondary"
                        size="sm"
                        disabled={comeBack.isPending}
                        onClick={() =>
                          comeBack.mutate({ date: place.date, start: place.start, end: place.end })
                        }
                      >
                        می‌آیم
                      </Button>
                    ) : (
                      !place.started && (
                        <div className="flex flex-wrap gap-2">
                          <Button variant="secondary" size="sm" onClick={() => setMoving(place)}>
                            جابه‌جایی
                          </Button>
                          {place.leave === "Pending" ? (
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={cancelLeave.isPending}
                              onClick={() =>
                                cancelLeave.mutate({
                                  date: place.date,
                                  start: place.start,
                                  end: place.end,
                                })
                              }
                            >
                              لغو درخواست
                            </Button>
                          ) : // By the day before (decided by the developer, 1405/08/19): today's
                          // button is shown greyed out, not pressable (asked by the developer).
                          place.date > mine.today ? (
                            <ConfirmDialog
                              label="درخواست مرخصی"
                              question={`از مربی برای سانس ${label} مرخصی می‌خواهید. تا مربی قبول نکند، جایتان در سانس می‌ماند. ادامه می‌دهید؟`}
                              pending={leave.isPending}
                              onConfirm={(close) =>
                                leave.mutate(
                                  { date: place.date, start: place.start, end: place.end },
                                  { onSettled: close },
                                )
                              }
                            />
                          ) : (
                            <Button variant="outline" size="sm" disabled>
                              درخواست مرخصی
                            </Button>
                          )}
                        </div>
                      )
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </PlacesByWeek>
      </CardContent>

      {moving !== null && (
        <MoveDialog
          key={moving.date + moving.start}
          place={moving}
          openings={mine.openings}
          onClose={() => setMoving(null)}
        />
      )}
    </Card>
  );
}

const openingKey = (opening: Opening) => opening.date + opening.start + opening.end;

/** «جابه‌جایی»: the trainer's sessions with room, by date; one picked, then moved to. */
function MoveDialog({
  place,
  openings,
  onClose,
}: {
  place: Place;
  openings: Opening[];
  onClose: () => void;
}) {
  const move = useMoveMySession();
  const [picked, setPicked] = useState<Opening | null>(null);
  const dates = [...new Set(openings.map((opening) => opening.date))];

  return (
    <Dialog open onOpenChange={(open) => !open && !move.isPending && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>جابه‌جایی</DialogTitle>
          <DialogDescription>
            سانس {formatDayLabel(place.date)}، {formatSessionTimes(place)} به کدام سانس برود؟ فقط
            همین یک روز جابه‌جا می‌شود.
          </DialogDescription>
        </DialogHeader>

        {openings.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            سانس دیگری با جای خالی تا آخرین روز برنامه‌تان نیست.
          </p>
        ) : (
          <ul aria-label="سانس‌های با جای خالی" className="space-y-3">
            {dates.map((date) => (
              <li key={date} className="space-y-1">
                <p className="text-sm font-semibold">{formatDayLabel(date)}</p>
                <div className="flex flex-wrap gap-1">
                  {openings
                    .filter((opening) => opening.date === date)
                    .map((opening) => {
                      const on = picked !== null && openingKey(picked) === openingKey(opening);
                      return (
                        <Button
                          key={openingKey(opening)}
                          size="sm"
                          variant={on ? "default" : "outline"}
                          aria-pressed={on}
                          aria-label={`${formatDayLabel(date)}، ${formatSessionTimes(opening)}${opening.isVip ? "، VIP" : ""}`}
                          disabled={move.isPending}
                          onClick={() => setPicked(opening)}
                        >
                          {formatSessionTimes(opening)}
                          {opening.isVip && <span className="text-xs font-semibold">VIP</span>}
                          <span className="text-xs opacity-80">
                            {toPersianDigits(opening.free)} جا
                          </span>
                        </Button>
                      );
                    })}
                </div>
              </li>
            ))}
          </ul>
        )}

        {move.isError && <Alert variant="destructive">{errorMessage(move.error)}</Alert>}

        <div className="flex flex-wrap gap-2">
          <Button
            disabled={picked === null || move.isPending}
            onClick={() =>
              picked !== null && move.mutate({ from: place, to: picked }, { onSuccess: onClose })
            }
          >
            {move.isPending ? "در حال ثبت…" : "جابه‌جایی به این سانس"}
          </Button>
          <Button variant="outline" disabled={move.isPending} onClick={onClose}>
            انصراف
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
