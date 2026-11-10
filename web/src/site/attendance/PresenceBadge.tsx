import { Badge } from "@/components/ui/badge";

import {
  formatCheckOut,
  formatClock,
  formatEarly,
  formatLate,
  presenceLabels,
  presenceVariants,
} from "./labels";
import type { SessionPresence } from "./api";

/** The API's whole-number fields may come as text in the generated types; the API sends numbers. */
type Minutes = number | string | null | undefined;

function minutesOf(value: Minutes): number | null {
  if (value === null || value === undefined) {
    return null;
  }

  const minutes = Number(value);

  return Number.isFinite(minutes) && minutes > 0 ? minutes : null;
}

/**
 * A student's state in one session with the check-in that counted: «به‌موقع ۱۳:۱۰», «بعد از سانس
 * آمد ۱۶:۰۰». Under a check-in, how early or late it was from the start («۱۰ دقیقه زودتر آمد»,
 * «۲۵ دقیقه تأخیر») and its check-out («خروج ۱۴:۴۰», «داخل است», «خروج ثبت نشد») (15.7d).
 * May wrap, so it never runs out of a narrow box.
 */
export function PresenceBadge({
  state,
  checkedInAt,
  checkedOutAt,
  autoCheckedOut = false,
  minutesEarly,
  minutesLate,
}: {
  state: SessionPresence;
  checkedInAt?: string | null;
  checkedOutAt?: string | null;
  autoCheckedOut?: boolean;
  minutesEarly?: Minutes;
  minutesLate?: Minutes;
}) {
  const time = checkedInAt === null || checkedInAt === undefined ? null : formatClock(checkedInAt);
  const early = minutesOf(minutesEarly);
  const late = minutesOf(minutesLate);
  const notes = [
    early !== null ? formatEarly(early) : null,
    late !== null ? formatLate(late) : null,
    time !== null ? formatCheckOut(checkedOutAt, autoCheckedOut) : null,
  ].filter((note) => note !== null);

  return (
    <span className="flex flex-col items-end gap-0.5">
      <Badge variant={presenceVariants[state]} className="max-w-full whitespace-normal">
        {presenceLabels[state]}
        {time !== null && ` ${time}`}
      </Badge>
      {notes.length > 0 && (
        <span className="text-xs text-muted-foreground">{notes.join(" · ")}</span>
      )}
    </span>
  );
}
