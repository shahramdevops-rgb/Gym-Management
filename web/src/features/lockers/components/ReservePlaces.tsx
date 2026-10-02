import { ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";

import type { CurrentlyInside } from "@/features/attendance/api";
import {
  cardioOnlyLabel,
  guestLabel,
  holderName,
  isGuestVisit,
} from "@/features/attendance/holder";
import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import { longStayLabel, stayProgress } from "../longStay";
import { birthdayLabel, Celebration } from "./Celebration";
import { doorFace, doorMotion, doorStateClass } from "./doorStyle";
import { StayBar } from "./StayBar";

/** How many visits can be inside with no locker at once: `Attendance.ReservePlaceCount` (BUSINESS_RULES.md §6). */
export const reservePlaceCount = 15;

/** A place is a door, a little wider than tall so a name fits on two lines. */
const placeSize = "flex h-20 w-28 items-center justify-center";

interface ReservePlacesProps {
  /** The open visits on a reserve place. */
  visits: CurrentlyInside[];
  /** Whether a locker is both in service and free, which rules a reserve place out. */
  anyLockerFree: boolean;
  /** The current moment, for how long each visit has gone on (BUSINESS_RULES.md §6 *Long stay*). */
  now: Date;
  /**
   * The visits a name search matched (BUSINESS_RULES.md §6 *Finding a member on the map*): the
   * places open by themselves to show them. `null` or absent while nothing is searched for.
   */
  foundAttendanceIds?: ReadonlySet<string> | null;
  /** The visits whose member's birthday is today: their places celebrate like a door does. */
  birthdayAttendanceIds?: ReadonlySet<string>;
  onOpenVisit: (visit: CurrentlyInside) => void;
  onCheckIn: () => void;
}

/**
 * The 15 reserve places (BUSINESS_RULES.md §6 *Reserve places*), tucked behind one small control
 * that says how many are used, with a ring that fills as they do. Every locker being full is rare,
 * and 15 boxes that are nearly always empty must not take the desk's room.
 *
 * Every place is drawn like a door on the map, from the same classes (`doorStyle`), and rises under
 * the mouse the same way. A used place looks like an occupied door, shows the member's name where a
 * locker would show its number, carries the same long-stay bar, and opens the same box as a locker.
 * An empty one looks like a free door and checks someone in only when no locker is free; until
 * then it is faded and says why not, rather than offering a check-in the API would refuse
 * (`Attendance.LockersStillFree`).
 */
export function ReservePlaces({
  visits,
  anyLockerFree,
  now,
  foundAttendanceIds = null,
  birthdayAttendanceIds,
  onOpenVisit,
  onCheckIn,
}: ReservePlacesProps) {
  const [open, setOpen] = useState(false);
  const empty = Math.max(0, reservePlaceCount - visits.length);
  const showsFound = foundAttendanceIds !== null && foundAttendanceIds.size > 0;
  const expanded = open || showsFound;

  return (
    <div className="space-y-3">
      <button
        type="button"
        aria-expanded={expanded}
        onClick={() => setOpen(!expanded)}
        className={cn(
          "inline-flex items-center gap-2.5 rounded-full border bg-card py-1.5 ps-2 pe-3.5 text-sm",
          "transition-[translate,border-color] duration-200 hover:-translate-y-0.5 hover:border-foreground/30",
          "focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none motion-reduce:hover:translate-y-0",
        )}
      >
        <UsageRing used={visits.length} />
        ورود بدون کمد {toPersianDigits(visits.length)} از {toPersianDigits(reservePlaceCount)}
        {expanded ? (
          <ChevronUp aria-hidden className="size-4" />
        ) : (
          <ChevronDown aria-hidden className="size-4" />
        )}
      </button>

      {expanded && (
        <div className="space-y-2 rounded-xl border bg-card p-3">
          {anyLockerFree && (
            <p className="text-sm text-muted-foreground">
              تا وقتی کمد آزادی هست، ورود بدون کمد ممکن نیست؛ کمد آزاد را از نقشه انتخاب کنید.
            </p>
          )}
          {/* dir="ltr", like the map, so each place's coloured edge is on the right as a door's is.
              The places have no numbers, so the order they run in says nothing. */}
          <ul
            aria-label="جاهای ورود بدون کمد"
            dir="ltr"
            className="flex flex-wrap gap-2 px-1 pt-2 pb-1"
          >
            {visits.map((visit) => {
              const stay = stayProgress(visit.checkedInAt, now);
              const found = foundAttendanceIds?.has(visit.attendanceId) ?? false;
              const birthday = birthdayAttendanceIds?.has(visit.attendanceId) ?? false;
              const guest = isGuestVisit(visit);
              const extras = [
                guest ? guestLabel : null,
                visit.isCardioOnly ? cardioOnlyLabel : null,
                birthday ? birthdayLabel : null,
                stay.isLong ? longStayLabel : null,
              ].filter((part) => part !== null);
              return (
                <li key={visit.attendanceId}>
                  <button
                    type="button"
                    aria-label={
                      extras.length > 0 ? [holderName(visit), ...extras].join("، ") : undefined
                    }
                    data-found={found || undefined}
                    data-birthday={birthday || undefined}
                    className={cn(
                      placeSize,
                      doorFace,
                      doorMotion,
                      guest
                        ? doorStateClass.guest
                        : visit.isCardioOnly
                          ? doorStateClass.cardio
                          : doorStateClass.occupied,
                      "px-2 text-center text-xs leading-tight font-medium",
                      birthday && "shadow-[0_0_18px_-4px_var(--party-pink)]",
                      found &&
                        "-translate-y-1 ring-2 ring-success ring-offset-2 ring-offset-card motion-reduce:translate-y-0",
                    )}
                    onClick={() => onOpenVisit(visit)}
                  >
                    {birthday && <Celebration />}
                    <span dir="rtl" className="relative line-clamp-2">
                      {holderName(visit)}
                    </span>
                    <StayBar progress={stay} />
                  </button>
                </li>
              );
            })}
            {Array.from({ length: empty }, (_, index) => (
              <li key={`empty-${index}`}>
                <button
                  type="button"
                  aria-label="جای خالی ورود بدون کمد"
                  disabled={anyLockerFree}
                  className={cn(
                    placeSize,
                    doorFace,
                    doorMotion,
                    doorStateClass.free,
                    "text-sm font-bold",
                  )}
                  onClick={onCheckIn}
                >
                  خالی
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/** A small ring that fills as the reserve places are used, drawn in SVG (no chart package). */
function UsageRing({ used }: { used: number }) {
  const radius = 15;
  const circumference = 2 * Math.PI * radius;
  const filled = (Math.min(used, reservePlaceCount) / reservePlaceCount) * circumference;

  return (
    <svg aria-hidden viewBox="0 0 36 36" className="size-6 -rotate-90">
      <circle cx="18" cy="18" r={radius} fill="none" strokeWidth="4" className="stroke-border" />
      {/* None used, no arc: a round cap on a zero-length dash would still draw a dot. */}
      {used > 0 && (
        <circle
          cx="18"
          cy="18"
          r={radius}
          fill="none"
          strokeWidth="4"
          strokeLinecap="round"
          strokeDasharray={`${filled} ${circumference}`}
          className={used >= reservePlaceCount ? "stroke-destructive" : "stroke-success"}
        />
      )}
    </svg>
  );
}
