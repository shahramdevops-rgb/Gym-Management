import { ChevronLeft, ChevronRight } from "lucide-react";

import { Button } from "@/components/ui/button";
import { toPersianDigits } from "@/lib/format";

import { formatDayMonth } from "./labels";

/**
 * «هفتهٔ ۲ از ۱۲» with the week's first and last dates, between «هفتهٔ قبل» and «هفتهٔ بعد». The
 * weeks are counted from today, seven days each, as the timetable's twelve weeks are.
 */
export function WeekNav({
  index,
  count,
  from,
  to,
  onChange,
}: {
  index: number;
  count: number;
  /** The week's first date; with `to`, shown under its number. */
  from?: string;
  to?: string;
  onChange: (index: number) => void;
}) {
  return (
    <div className="flex items-center justify-between gap-2">
      <Button
        variant="ghost"
        size="sm"
        aria-label="هفتهٔ قبل"
        disabled={index === 0}
        onClick={() => onChange(index - 1)}
      >
        <ChevronRight aria-hidden />
      </Button>
      <p className="text-sm font-semibold" aria-live="polite">
        هفتهٔ {toPersianDigits(index + 1)} از {toPersianDigits(count)}
        {from !== undefined && to !== undefined && (
          <span className="block text-center text-xs font-normal text-muted-foreground">
            {formatDayMonth(from)} تا {formatDayMonth(to)}
          </span>
        )}
      </p>
      <Button
        variant="ghost"
        size="sm"
        aria-label="هفتهٔ بعد"
        disabled={index >= count - 1}
        onClick={() => onChange(index + 1)}
      >
        <ChevronLeft aria-hidden />
      </Button>
    </div>
  );
}
