import { Cake, RefreshCw } from "lucide-react";
import type { ReactNode } from "react";

import type { CurrentlyInside } from "@/features/attendance/api";
import { renewalDue, type RenewalDue } from "@/features/attendance/renewal";
import { gymToday, isJalaliBirthday, toPersianDigits } from "@/lib/format";

interface DeskPanelProps {
  /** Everyone inside now, in the order they came in. */
  visits: CurrentlyInside[];
  /** An entry was clicked: open its locker's box. */
  onOpen: (visit: CurrentlyInside) => void;
  /** The locker to blink on the map while an entry is pointed at; `null` when none is. */
  onHighlight: (lockerId: string | null) => void;
}

/**
 * The desk panel beside the map (BUSINESS_RULES.md §6 *The desk panel*): the members inside now
 * whose Jalali birthday is today, and those whose subscription is running out, so the desk says
 * happy birthday, or tells them to renew, while they are standing there.
 *
 * Each list shows only when it has someone in it, and the panel renders nothing when neither does:
 * a card that says "nobody" every day would teach the desk to stop looking at it.
 */
export function DeskPanel({ visits, onOpen, onHighlight }: DeskPanelProps) {
  // The gym's day, not the browser's: the same definition IGymCalendar.Today() uses on the API.
  const today = gymToday();

  const birthdays = visits.filter((visit) => isJalaliBirthday(visit.memberBirthDate, today));
  const renewals = visits.flatMap((visit) => {
    const due = renewalDue(visit, today);
    return due === null ? [] : [{ visit, due }];
  });

  if (birthdays.length === 0 && renewals.length === 0) {
    return null;
  }

  const entry = (visit: CurrentlyInside, detail?: ReactNode) => (
    <li key={visit.attendanceId}>
      <button
        type="button"
        onClick={() => onOpen(visit)}
        onMouseEnter={() => onHighlight(visit.lockerId)}
        onMouseLeave={() => onHighlight(null)}
        onFocus={() => onHighlight(visit.lockerId)}
        onBlur={() => onHighlight(null)}
        className="flex w-full items-center gap-2 rounded-sm px-2 py-1 text-start text-sm transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
      >
        <span className="min-w-0 flex-1 truncate font-medium">{visit.memberFullName}</span>
        <span className="shrink-0 text-xs text-muted-foreground">{placeLabel(visit)}</span>
        {detail}
      </button>
    </li>
  );

  return (
    <aside
      aria-label="پنل پذیرش"
      dir="rtl"
      // As tall as the three doors of the cabinets beside it, and scrolling inside beyond that,
      // so a busy day never pushes the map down.
      className="flex max-h-[calc(3*var(--door)+1.25rem)] w-72 max-w-full flex-col gap-2 overflow-y-auto"
    >
      {birthdays.length > 0 && (
        <section
          aria-label="تولدت مبارک"
          className="rounded-md border border-primary/50 bg-primary/10 p-2"
        >
          <h3 className="mb-1 flex items-center gap-2 px-2 text-sm font-bold">
            <Cake aria-hidden className="size-4 text-primary" />
            تولدت مبارک
          </h3>
          <ul>{birthdays.map((visit) => entry(visit))}</ul>
        </section>
      )}

      {renewals.length > 0 && (
        <section
          aria-label="فرصت تمدید"
          className="rounded-md border border-warning/50 bg-warning/10 p-2"
        >
          <h3 className="mb-1 flex items-center gap-2 px-2 text-sm font-bold">
            <RefreshCw aria-hidden className="size-4 text-warning" />
            فرصت تمدید ({toPersianDigits(renewals.length)})
          </h3>
          <ul>
            {renewals.map(({ visit, due }) =>
              entry(
                visit,
                <span className="shrink-0 text-xs font-medium whitespace-nowrap text-warning">
                  {dueLabel(due)}
                </span>,
              ),
            )}
          </ul>
        </section>
      )}
    </aside>
  );
}

/** Where the member is: their locker, or «رزرو» for a reserve place, which has no number (§6). */
function placeLabel(visit: CurrentlyInside): string {
  return visit.lockerNumber !== null ? `کمد ${toPersianDigits(visit.lockerNumber)}` : "رزرو";
}

function dueLabel(due: RenewalDue): string {
  if (due.kind === "sessions") {
    return due.left <= 0 ? "جلسه‌ها تمام شد" : `${toPersianDigits(due.left)} جلسه مانده`;
  }

  return due.left <= 0 ? "امروز تمام می‌شود" : `${toPersianDigits(due.left)} روز مانده`;
}
