import { Cake, RefreshCw } from "lucide-react";
import type { ReactNode } from "react";

import type { CurrentlyInside } from "@/features/attendance/api";
import { holderName } from "@/features/attendance/holder";
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

  const entry = (visit: CurrentlyInside, detail: ReactNode) => (
    <li key={visit.attendanceId}>
      <button
        type="button"
        onClick={() => onOpen(visit)}
        onMouseEnter={() => onHighlight(visit.lockerId)}
        onMouseLeave={() => onHighlight(null)}
        onFocus={() => onHighlight(visit.lockerId)}
        onBlur={() => onHighlight(null)}
        className="flex w-full items-center gap-3 rounded-lg border border-door-border px-2.5 py-2 text-start text-sm transition-[translate,background-color,border-color] duration-200 hover:-translate-y-0.5 hover:border-foreground/25 hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none motion-reduce:hover:translate-y-0"
      >
        <Initials name={holderName(visit)} />
        <span className="grid min-w-0 flex-1">
          <span className="truncate font-bold">{holderName(visit)}</span>
          <span className="text-xs text-muted-foreground">{placeLabel(visit)}</span>
        </span>
        {detail}
      </button>
    </li>
  );

  return (
    <aside
      aria-label="پنل پذیرش"
      dir="rtl"
      // As tall as the three doors of the cabinets beside it, and scrolling inside beyond that,
      // so a busy day never pushes the map down. The line across its top is drawn by `before:`.
      className="relative flex max-h-[calc(3*var(--door-size)+1.25rem)] w-80 max-w-full flex-col gap-4 overflow-y-auto rounded-xl border bg-card p-3 pt-4 before:absolute before:inset-x-0 before:top-0 before:h-[3px] before:bg-linear-to-l before:from-success before:to-primary before:content-['']"
    >
      {birthdays.length > 0 && (
        // A plain list, like the renewals: the party is drawn on the member's door, not here.
        <section aria-label="تولدت مبارک">
          <h3 className="mb-2 flex items-center gap-2 px-1 text-sm font-bold text-party-pink">
            <Cake aria-hidden className="size-4" />
            تولدت مبارک
          </h3>
          <ul className="space-y-2">
            {birthdays.map((visit) =>
              entry(
                visit,
                <span className="shrink-0 text-xs font-bold whitespace-nowrap text-party-pink">
                  امروز تولدشه
                </span>,
              ),
            )}
          </ul>
        </section>
      )}

      {renewals.length > 0 && (
        <section aria-label="فرصت تمدید">
          <h3 className="mb-2 flex items-center gap-2 px-1 text-sm font-bold text-warning">
            <RefreshCw aria-hidden className="size-4" />
            فرصت تمدید ({toPersianDigits(renewals.length)})
          </h3>
          <ul className="space-y-2">
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

/**
 * The member's initials in a circle, the first letter of their first and last names, kept apart
 * by a zero-width non-joiner so they read as two letters, not a word. The circle's hue comes from
 * the name, so the same member always gets the same one; it only helps the eye, so it is hidden
 * from a screen reader.
 */
function Initials({ name }: { name: string }) {
  const words = name.split(" ").filter((word) => word !== "");
  const first = words[0]?.[0] ?? "";
  const last = words.length > 1 ? (words[words.length - 1]?.[0] ?? "") : "";
  const letters = last === "" ? first : [first, last].join("\u200C");
  const hue = [...name].reduce((sum, character) => sum + character.codePointAt(0)!, 0) % 360;

  return (
    <span
      aria-hidden
      className="grid size-8 shrink-0 place-items-center rounded-full text-xs font-extrabold text-white"
      style={{ backgroundColor: `oklch(0.5 0.13 ${hue})` }}
    >
      {letters}
    </span>
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
