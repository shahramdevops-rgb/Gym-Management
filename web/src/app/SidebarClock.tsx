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
    <div data-testid="sidebar-clock" className="rounded-lg border bg-background/40 px-3 py-2">
      <p className="text-2xl leading-tight font-extrabold tabular-nums">
        {formatTime(now.toISOString())}
      </p>
      <p className="text-xs text-muted-foreground">{formatLongDate(now)}</p>
    </div>
  );
}
