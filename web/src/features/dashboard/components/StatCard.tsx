import { ArrowDown, ArrowUp, Minus, type LucideIcon } from "lucide-react";

import { stateEdge } from "@/lib/edges";
import { formatPercent } from "@/lib/format";
import { cn } from "@/lib/utils";

import { percentChange, type Outcome } from "../figures";
import { toneStyle, type Tone } from "@/lib/tone";

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
  /**
   * The card's edge and icon in a colour of its own. Without one, the page's: «خلاصهٔ بازه» is all
   * in «مالی»'s violet (13.2i, asked by the developer).
   */
  tone?: Tone;
  icon: LucideIcon;
  /** One line under the figure: what it counts, or what it is made of. */
  hint?: string;
  comparison?: Comparison;
  /** A share out of 100 drawn as a thin bar under the figure (a rate); null draws none. */
  progress?: number | null;
  /**
   * The figure in green or red: a profit or a loss (BUSINESS_RULES.md §12 *Dashboard*). The sign
   * of the figure itself says which, so the colour is never alone.
   */
  outcome?: Outcome;
  /**
   * A few figures the main one is made of, listed in the card's empty corner under the icon,
   * beside the figure rather than under it (asked by the developer, 1405/07/14).
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
 * together, never the colour alone. Edged like a page's `StatTile` and the banner (13.2i): a faint
 * frame and a thick start edge in the card's tone, or the page's without one; no strip, shadow
 * or lift.
 *
 * The label and the figure are the card's own `dt` and `dd`, so the card stays one group of its
 * list; the icon sits inside the `dt`, placed at the card's top corner. A breakdown is one more
 * `dd`, placed in the column under the icon; the figure and its lines keep clear of it.
 */
export function StatCard({
  label,
  value,
  tone,
  icon: Icon,
  hint,
  comparison,
  progress,
  outcome,
  breakdown,
}: StatCardProps) {
  const besideBreakdown = breakdown !== undefined && "pe-44";

  return (
    <div
      style={tone === undefined ? undefined : toneStyle(tone)}
      className={cn(
        "relative space-y-1.5 overflow-hidden rounded-xl border border-s-[3px] tone-edge bg-card p-4",
        breakdown !== undefined && "min-h-40",
      )}
    >
      <dt className="pe-12 text-sm text-muted-foreground">
        {label}
        <span
          aria-hidden
          className="absolute end-4 top-4 grid size-10 place-items-center rounded-xl tone-soft tone-ink"
        >
          <Icon className="size-5" />
        </span>
      </dt>
      <dd
        className={cn(
          "pe-12 text-2xl font-extrabold tracking-tight",
          besideBreakdown,
          outcome === "gain" && "text-success-ink",
          outcome === "loss" && "text-destructive",
        )}
      >
        {value}
      </dd>
      {breakdown !== undefined && (
        <dd className="absolute end-4 top-16 bottom-4 w-40">
          <ul className="flex h-full flex-col justify-evenly text-xs">
            {breakdown.map((row) => (
              <li key={row.label} className="flex items-baseline justify-between gap-2">
                <span className="text-muted-foreground">{row.label}</span>
                <span className="font-semibold whitespace-nowrap tabular-nums">{row.value}</span>
              </li>
            ))}
          </ul>
        </dd>
      )}
      {progress !== undefined && progress !== null && (
        <dd
          aria-hidden
          className={cn(
            "h-1.5 overflow-hidden rounded-full bg-muted",
            breakdown !== undefined && "me-44",
          )}
        >
          <div
            className="h-full rounded-full tone-bar"
            style={{ inlineSize: `${Math.min(100, Math.max(0, progress))}%` }}
          />
        </dd>
      )}
      {comparison !== undefined && (
        <ChangeLine comparison={comparison} className={breakdown !== undefined && "me-44"} />
      )}
      {hint !== undefined && (
        <dd className={cn("text-xs text-muted-foreground", besideBreakdown)}>{hint}</dd>
      )}
    </div>
  );
}

/** Where the cards will be while the figures load: their shapes, pulsing, instead of a line of text. */
export function StatCardsLoading() {
  return (
    <div role="status" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <span className="sr-only">در حال بارگذاری…</span>
      {[0, 1, 2, 3, 4, 5].map((index) => (
        <div
          key={index}
          aria-hidden
          className="space-y-3 rounded-xl border border-s-[3px] tone-edge bg-card p-4 motion-safe:animate-pulse"
        >
          <div className="flex justify-between">
            <div className="h-4 w-24 rounded-full bg-muted" />
            <div className="size-10 rounded-xl bg-muted" />
          </div>
          <div className="h-7 w-40 rounded-full bg-muted" />
          <div className="h-4 w-32 rounded-full bg-muted" />
        </div>
      ))}
    </div>
  );
}

/** Edged like a row's tag (13.2i): no fill, the thick edge at its end in the change's colour. */
const badge = "inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-xs font-medium";

/**
 * The change since the range before. Beside a breakdown it keeps clear of that column (a margin,
 * not padding, so a pill wraps its words instead of growing into the column): «بدون تغییر نسبت به
 * بازهٔ قبل» ran into «انتقال» on a 1440px screen (13.2d).
 */
function ChangeLine({
  comparison,
  className,
}: {
  comparison: Comparison;
  className?: string | false;
}) {
  const change = percentChange(comparison.current, comparison.previous);

  if (change === null) {
    return (
      <dd className={cn("text-xs text-muted-foreground", className)}>
        بازهٔ قبل: {comparison.previousLabel}
      </dd>
    );
  }

  const rounded = Math.round(change);
  if (rounded === 0) {
    return (
      <dd className={cn(badge, stateEdge.muted, className)}>
        <Minus className="size-3.5" aria-hidden />
        بدون تغییر نسبت به بازهٔ قبل
      </dd>
    );
  }

  const up = rounded > 0;
  const good = up === (comparison.higherIsBetter ?? true);
  const Icon = up ? ArrowUp : ArrowDown;

  return (
    <dd className={cn(badge, good ? stateEdge.success : stateEdge.destructive, className)}>
      <Icon className="size-3.5" aria-hidden />
      {formatPercent(Math.abs(change))} {up ? "بیشتر" : "کمتر"} از بازهٔ قبل
    </dd>
  );
}
