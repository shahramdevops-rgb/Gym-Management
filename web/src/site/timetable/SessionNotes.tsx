import { CalendarDays, Pin } from "lucide-react";

import { cn } from "@/lib/utils";

/**
 * The trainer's notes on a session (BUSINESS_RULES.md §15 *Session notes*, roadmap 15.7e), each on
 * a line of its own under the session: the standing note («یادداشت ثابت», every week) and the note
 * on this date alone («یادداشت این روز»). Nothing when there is neither.
 */
export function SessionNotes({
  note,
  dateNote,
  compact = false,
  className,
}: {
  note?: string | null;
  dateNote?: string | null;
  /** On a small box (the home's «سانس‌های من»): two lines at most for each note. */
  compact?: boolean;
  className?: string;
}) {
  if (!note && !dateNote) {
    return null;
  }

  const text = cn("min-w-0 whitespace-pre-line break-words", compact && "line-clamp-2");

  return (
    <div className={cn("space-y-1 text-xs", className)}>
      {note && (
        <p className="flex items-start gap-1 text-muted-foreground">
          <Pin aria-hidden className="mt-0.5 size-3 shrink-0" />
          <span className="sr-only">یادداشت ثابت: </span>
          <span className={text}>{note}</span>
        </p>
      )}
      {dateNote && (
        <p className="flex items-start gap-1 rounded-md bg-primary/8 px-1.5 py-1 text-foreground">
          <CalendarDays aria-hidden className="mt-0.5 size-3 shrink-0 text-primary" />
          <span className="sr-only">یادداشت این روز: </span>
          <span className={text}>{dateNote}</span>
        </p>
      )}
    </div>
  );
}
