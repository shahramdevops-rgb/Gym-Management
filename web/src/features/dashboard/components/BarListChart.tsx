import type { LucideIcon } from "lucide-react";

import { formatNumber } from "@/lib/format";
import { cn } from "@/lib/utils";

import { toneColorStyle, type Tone } from "../tone";
import { ChartCard } from "./ChartCard";

/** How many of a bar's thing were sold in the range, shown on the bar itself. */
export interface BarListSold {
  count: number;
  /** What one of them is called: «پلن», «تک‌جلسه». */
  noun: string;
}

/** One bar: a source, a method, a category or a product. */
export interface BarListItem {
  key: string;
  name: string;
  value: number;
  /** A second figure under the value (a product's sales in Toman beside how many were sold). */
  detail?: string;
  /** A count written on the bar: «۱۲ پلن فروخته شد». */
  sold?: BarListSold;
}

interface BarListChartProps {
  title: string;
  description?: string;
  items: BarListItem[];
  /** The exact value, written at the bar's tip. */
  formatValue: (value: number) => string;
  icon: LucideIcon;
  /** The chart's accent, its icon's and its bars' colour. */
  tone: Tone;
  /**
   * The bars' colour when it is not the tone's: money in and money out are drawn in the two series
   * colours of the revenue chart (`var(--chart-1)`, `var(--chart-2)`), so expenses never read as
   * revenue.
   */
  color?: string;
}

/**
 * Past this share of the longest bar, a bar is wide enough to carry its count inside it; a shorter
 * one has it right after its tip, so the words are never cut off by their own bar.
 */
const countFitsInsideFrom = 55;

/** The count on a bar: a small light pill, readable on the bar's colour and on the card alike. */
function SoldPill({ sold }: { sold: BarListSold }) {
  return (
    <span className="shrink-0 rounded-full border bg-card px-2 py-px text-[11px] leading-4 font-medium whitespace-nowrap text-card-foreground shadow-sm">
      {formatNumber(sold.count)} {sold.noun} فروخته شد
    </span>
  );
}

/**
 * Named things side by side, one bar each, growing from the start (the right) the way the page
 * reads, with the exact value written beside every bar. One series, so one colour and no legend:
 * the title says what is measured.
 *
 * A bar can also carry how many were sold (plans and single visits, asked by the developer,
 * 1405/07/13). That count follows the sale's own day, not the money's, so it is written in words
 * on the bar rather than drawn: the bar's length stays the money received.
 *
 * Plain HTML rather than Recharts: a few rows with their value at the tip need no axis, no
 * tooltip and no hover to be read, and an HTML row keeps Persian text, its digits and the
 * right-to-left layout in the order the browser already gets right, where an SVG chart has to be
 * turned around by hand. A negative net (more refunded than received) draws no bar; its value
 * still says so.
 */
export function BarListChart({
  title,
  description,
  items,
  formatValue,
  icon,
  tone,
  color = `var(--tone-${tone})`,
}: BarListChartProps) {
  const largest = Math.max(0, ...items.map((item) => item.value));
  // A range with plans sold and nothing yet paid still has something to say.
  const empty = items.every((item) => item.value === 0 && (item.sold?.count ?? 0) === 0);

  return (
    <ChartCard title={title} description={description} icon={icon} tone={tone} empty={empty}>
      <ul className="space-y-3" style={toneColorStyle(color)}>
        {items.map((item) => {
          const percent = largest > 0 ? Math.max(0, (item.value / largest) * 100) : 0;
          const sold = item.sold;
          const countInside = sold !== undefined && percent >= countFitsInsideFrom;
          return (
            <li key={item.key} className="grid grid-cols-[7rem_1fr] items-center gap-3 text-sm">
              <span className="truncate" title={item.name}>
                {item.name}
              </span>
              <div className="flex min-w-0 items-center gap-2">
                <div
                  className={cn(
                    "flex min-w-0 flex-1 items-center gap-1.5 rounded-full bg-muted/70",
                    sold === undefined ? "h-4" : "h-6",
                  )}
                >
                  {percent > 0 && (
                    <div
                      className="flex h-full items-center rounded-full px-1 tone-bar shadow-xs"
                      style={{ inlineSize: `${percent}%` }}
                    >
                      {sold !== undefined && countInside && <SoldPill sold={sold} />}
                    </div>
                  )}
                  {sold !== undefined && !countInside && <SoldPill sold={sold} />}
                </div>
                <div className="shrink-0 text-end text-xs tabular-nums">
                  <p className="font-medium">{formatValue(item.value)}</p>
                  {item.detail !== undefined && (
                    <p className="text-muted-foreground">{item.detail}</p>
                  )}
                </div>
              </div>
            </li>
          );
        })}
      </ul>
    </ChartCard>
  );
}
