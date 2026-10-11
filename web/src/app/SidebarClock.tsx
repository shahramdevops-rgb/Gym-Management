import { formatLongDate, formatTime } from "@/lib/format";
import { useNow } from "@/lib/useNow";

/**
 * The time and today's date at the top of the side menu, on every screen. Both are the gym's
 * (Tehran), not the browser's, like every business date (BUSINESS_RULES.md §0).
 *
 * It ticks every second on its own, so only these two lines re-render, never the page beside it.
 */
export function SidebarClock() {
  const now = useNow(1_000);

  return (
    <div
      data-testid="sidebar-clock"
      className="flex items-baseline justify-between gap-2 rounded-xl border bg-card px-3 py-2 shadow-card"
    >
      <p className="text-xl leading-tight font-extrabold tabular-nums">
        {formatTime(now.toISOString())}
      </p>
      <p className="text-xs text-muted-foreground">{formatLongDate(now)}</p>
    </div>
  );
}
