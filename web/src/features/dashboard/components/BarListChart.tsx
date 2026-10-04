import { ChartCard } from "./ChartCard";

/** One bar: a source, a method, a category or a product. */
export interface BarListItem {
  key: string;
  name: string;
  value: number;
  /** A second figure under the value (a product's sales in Toman beside how many were sold). */
  detail?: string;
}

interface BarListChartProps {
  title: string;
  description?: string;
  items: BarListItem[];
  /** The exact value, written at the bar's tip. */
  formatValue: (value: number) => string;
  /** Money out is drawn in the expenses colour, so it never reads as revenue. */
  color?: "var(--chart-1)" | "var(--chart-2)";
}

/**
 * Named things side by side, one bar each, growing from the start (the right) the way the page
 * reads, with the exact value written beside every bar. One series, so one colour and no legend:
 * the title says what is measured.
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
  color = "var(--chart-1)",
}: BarListChartProps) {
  const largest = Math.max(0, ...items.map((item) => item.value));

  return (
    <ChartCard
      title={title}
      description={description}
      empty={items.every((item) => item.value === 0)}
    >
      <ul className="space-y-2">
        {items.map((item) => {
          const percent = largest > 0 ? Math.max(0, (item.value / largest) * 100) : 0;
          return (
            <li key={item.key} className="grid grid-cols-[7rem_1fr] items-center gap-3 text-sm">
              <span className="truncate" title={item.name}>
                {item.name}
              </span>
              <div className="flex min-w-0 items-center gap-2">
                <div className="h-5 min-w-0 flex-1">
                  {percent > 0 && (
                    <div
                      aria-hidden
                      className="h-full rounded-e-sm"
                      style={{ inlineSize: `${percent}%`, background: color }}
                    />
                  )}
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
