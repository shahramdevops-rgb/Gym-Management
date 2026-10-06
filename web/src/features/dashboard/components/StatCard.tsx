import { ArrowDown, ArrowUp, Minus } from "lucide-react";

import { formatPercent } from "@/lib/format";
import { cn } from "@/lib/utils";

import { percentChange, type Outcome } from "../figures";

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
  /**
   * The figure in green or red: a profit or a loss (BUSINESS_RULES.md §12 *Dashboard*). The sign
   * of the figure itself says which, so the colour is never alone.
   */
  outcome?: Outcome;
  /**
   * A few figures the main one is made of, listed beside the figure rather than under it (asked by
   * the developer, 1405/07/14).
   */
  breakdown?: BreakdownRow[];
}

/** One line of a card's breakdown: «کارت ۳۰٬۰۰۰٬۰۰۰ تومان». */
export interface BreakdownRow {
  label: string;
  value: string;
}

/**
 * One headline figure. The change since the range before is an arrow, a word and a colour
 * together, never the colour alone.
 *
 * Plain on purpose: the coloured design (accents, icons, a banner) is kept under the git tag
 * `dashboard-colour-v1` for the later UI update (docs/design/dashboard-colour.md).
 *
 * The label and the figure are the card's own `dt` and `dd`, so the card stays one group of its
 * list. A breakdown is one more `dd`, in the card's second half beside all the lines; a figure too
 * wide for its line drops under its label instead of leaving the card.
 */
export function StatCard({ label, value, hint, comparison, outcome, breakdown }: StatCardProps) {
  const lines = 2 + (comparison === undefined ? 0 : 1) + (hint === undefined ? 0 : 1);

  return (
    <div
      className={cn(
        "grid content-start gap-y-1 rounded-xl border bg-card p-4 shadow-sm",
        breakdown !== undefined && "grid-cols-2 gap-x-4",
      )}
    >
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd
        className={cn(
          "text-xl font-bold",
          outcome === "gain" && "text-success",
          outcome === "loss" && "text-destructive",
        )}
      >
        {value}
      </dd>
      {comparison !== undefined && <ChangeLine comparison={comparison} />}
      {hint !== undefined && <dd className="text-xs text-muted-foreground">{hint}</dd>}
      {breakdown !== undefined && (
        <dd className="col-start-2 min-w-0 border-s ps-3" style={{ gridRow: `1 / span ${lines}` }}>
          <ul className="flex h-full flex-col justify-evenly gap-1 text-xs">
            {breakdown.map((row) => (
              <li key={row.label} className="flex flex-wrap items-baseline justify-between gap-x-2">
                <span className="text-muted-foreground">{row.label}</span>
                <span className="font-semibold tabular-nums">{row.value}</span>
              </li>
            ))}
          </ul>
        </dd>
      )}
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
