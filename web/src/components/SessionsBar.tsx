import { Badge } from "@/components/ui/badge";
import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

/**
 * The generated API types widen an integer to `number | string`, so the component takes the wide
 * type and narrows it once, here, rather than making every caller remember to.
 */
type SessionCount = number | string | null | undefined;

interface SessionsBarProps {
  /** null means unlimited: there is no denominator, so there is no bar to draw. */
  total: SessionCount;
  used: SessionCount;
  /** null means unlimited. */
  remaining: SessionCount;
  /** At or below this many sessions left, the bar asks for attention (BUSINESS_RULES.md §7). */
  lowThreshold: number;
  className?: string;
}

function toCount(value: SessionCount): number | null {
  if (value === null || value === undefined) {
    return null;
  }

  const parsed = Number(value);

  return Number.isFinite(parsed) ? parsed : null;
}

/**
 * How much of a subscription is left, as "used of total" over a bar.
 *
 * An unlimited subscription says so instead: a progress bar with no denominator would have to
 * invent one, and a full bar and an empty bar would both be lies.
 *
 * Shared rather than local to attendance because Phase 9's dashboard shows the same thing, and a
 * second copy would drift from this one.
 */
export function SessionsBar({ total, used, remaining, lowThreshold, className }: SessionsBarProps) {
  const totalCount = toCount(total);
  const remainingCount = toCount(remaining);
  const usedCount = toCount(used) ?? 0;

  if (totalCount === null || remainingCount === null) {
    return <Badge variant="secondary">نامحدود</Badge>;
  }

  const isLow = remainingCount <= lowThreshold;
  // A total of zero cannot happen (a limited plan has at least one session), but dividing by it
  // would render a NaN width rather than fail loudly, so it is handled rather than assumed.
  const percent = totalCount > 0 ? Math.min(100, Math.round((usedCount / totalCount) * 100)) : 0;

  return (
    <div className={cn("min-w-24 space-y-1", className)}>
      <div className="flex items-center gap-1.5 text-sm tabular-nums">
        <span className={cn(isLow && "font-medium text-warning")}>
          {toPersianDigits(usedCount)} از {toPersianDigits(totalCount)}
        </span>
      </div>
      <div
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={totalCount}
        aria-valuenow={usedCount}
        aria-label={`${toPersianDigits(usedCount)} جلسه از ${toPersianDigits(totalCount)} مصرف شده`}
        className="h-1.5 w-full overflow-hidden rounded-full bg-muted"
      >
        <div
          className={cn("h-full rounded-full", isLow ? "bg-warning" : "bg-primary")}
          style={{ inlineSize: `${percent}%` }}
        />
      </div>
    </div>
  );
}
