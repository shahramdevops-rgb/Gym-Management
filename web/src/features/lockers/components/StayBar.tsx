import { cn } from "@/lib/utils";

import type { StayProgress } from "../longStay";

/**
 * The thin bar along the bottom of an occupied door or a used reserve place (BUSINESS_RULES.md §6
 * *Long stay*). It fills from the right, the way a Persian reader expects progress to go, in the
 * door's own red, and turns wholly the warning colour at three hours. It takes no corner and no
 * line of text; the button it sits in says it to a screen reader.
 *
 * `dir="rtl"` is set on it because the map is `dir="ltr"` (the cabinets stand left to right), and
 * the fill is placed with `start-0`.
 */
export function StayBar({ progress }: { progress: StayProgress }) {
  return (
    <span
      aria-hidden
      dir="rtl"
      data-testid="stay-bar"
      data-long={progress.isLong || undefined}
      className="pointer-events-none absolute inset-x-0 bottom-0 h-1 bg-muted-foreground/15"
    >
      <span
        data-testid="stay-bar-fill"
        className={cn(
          "absolute start-0 inset-y-0",
          progress.isLong ? "bg-warning" : "bg-destructive/60",
        )}
        style={{ width: `${Math.round(progress.fraction * 100)}%` }}
      />
    </span>
  );
}
