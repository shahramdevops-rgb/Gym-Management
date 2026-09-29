import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import { unusedLabel, usageLevels, usagePeriods, type UsagePeriod } from "../usage";
import { usageDoorClass } from "./doorStyle";

/** The swatch every door-shaped legend mark shares with the stats strip's. */
const swatch =
  "inline-block size-3.5 rounded-[3px] border border-e-[3px] border-door-border bg-door";

/**
 * The legend's row while the map shows how often each locker was used (BUSINESS_RULES.md §6
 * *Locker usage map*), in place of the free, occupied and out-of-service counts: the period picker,
 * the shades from least to most used with the most uses any locker had, and the colour of a locker
 * nobody used. Nothing on it is desk work.
 */
export function UsageLegend({
  period,
  onPeriodChange,
  mostUses,
}: {
  period: UsagePeriod;
  onPeriodChange: (period: UsagePeriod) => void;
  /** The most visits any one locker had in the period, or `null` while the counts are loading. */
  mostUses: number | null;
}) {
  return (
    <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
      <div
        role="group"
        aria-label="بازهٔ زمانی"
        className="flex rounded-lg border bg-background p-0.5"
      >
        {usagePeriods.map((days) => (
          <button
            key={days}
            type="button"
            aria-pressed={days === period}
            onClick={() => onPeriodChange(days)}
            className={cn(
              "rounded-md px-3 py-1 text-xs transition-colors",
              days === period
                ? "bg-door-use font-bold text-white"
                : "text-muted-foreground hover:text-foreground",
            )}
          >
            {toPersianDigits(days)} روز اخیر
          </button>
        ))}
      </div>
      <ul
        aria-label="راهنمای نقشهٔ استفاده"
        className="flex flex-wrap items-center gap-x-6 gap-y-2"
      >
        <li className="flex items-center gap-2 text-xs text-muted-foreground">
          کم
          <span aria-hidden className="flex gap-0.5">
            {Array.from({ length: usageLevels }, (_, index) => (
              <span key={index} className={cn(swatch, usageDoorClass[index + 1])} />
            ))}
          </span>
          زیاد
          {mostUses !== null && mostUses > 0 && (
            <span>(بیشترین {toPersianDigits(mostUses)} بار)</span>
          )}
        </li>
        <li className="flex items-center gap-2 text-xs text-muted-foreground">
          <span aria-hidden className={cn(swatch, usageDoorClass[0])} />
          {unusedLabel}
        </li>
      </ul>
    </div>
  );
}
