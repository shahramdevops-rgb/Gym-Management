import { ArrowDown, ArrowUp, Minus } from "lucide-react";

import { formatPercent } from "@/lib/format";
import { cn } from "@/lib/utils";

import { percentChange } from "../figures";

/** The range before, for a card that compares with it (§12: the same length, ending the day before). */
export interface Comparison {
  current: number | string;
  previous: number | string;
  /** The figure before as it is shown, for when a percent says nothing (it was zero or a loss). */
  previousLabel: string;
  /** Expenses going up is bad news: the arrow's colour follows the meaning, not the direction. */
  higherIsBetter?: boolean;
}

interface StatCardProps {
  label: string;
  value: string;
  /** One line under the figure: what it counts, or what it is made of. */
  hint?: string;
  comparison?: Comparison;
}

/**
 * One headline figure. The change since the range before is an arrow, a word and a colour
 * together, never the colour alone.
 */
export function StatCard({ label, value, hint, comparison }: StatCardProps) {
  return (
    <div className="space-y-1 rounded-xl border bg-card p-4 shadow-sm">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="text-xl font-bold">{value}</dd>
      {comparison !== undefined && <ChangeLine comparison={comparison} />}
      {hint !== undefined && <dd className="text-xs text-muted-foreground">{hint}</dd>}
    </div>
  );
}

function ChangeLine({ comparison }: { comparison: Comparison }) {
  const change = percentChange(comparison.current, comparison.previous);

  if (change === null) {
    return <dd className="text-xs text-muted-foreground">بازهٔ قبل: {comparison.previousLabel}</dd>;
  }

  const rounded = Math.round(change);
  if (rounded === 0) {
    return (
      <dd className="flex items-center gap-1 text-xs text-muted-foreground">
        <Minus className="size-3.5" aria-hidden />
        بدون تغییر نسبت به بازهٔ قبل
      </dd>
    );
  }

  const up = rounded > 0;
  const good = up === (comparison.higherIsBetter ?? true);
  const Icon = up ? ArrowUp : ArrowDown;

  return (
    <dd
      className={cn("flex items-center gap-1 text-xs", good ? "text-success" : "text-destructive")}
    >
      <Icon className="size-3.5" aria-hidden />
      {formatPercent(Math.abs(change))} {up ? "بیشتر" : "کمتر"} از بازهٔ قبل
    </dd>
  );
}
