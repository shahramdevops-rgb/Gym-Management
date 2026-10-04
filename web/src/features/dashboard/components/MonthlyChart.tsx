import { Bar, BarChart, CartesianGrid, LabelList, Tooltip, XAxis, YAxis } from "recharts";

import { axisTick, gridStroke, hoveredRow, svgText } from "../chartStyle";
import { ChartCard, ChartTooltip, type TooltipRow } from "./ChartCard";

/** One Jalali month's column. Null is a month with nothing to say (no plan decided yet). */
export interface MonthlyPoint {
  key: string;
  label: string;
  value: number | null;
  details?: TooltipRow[];
}

interface MonthlyChartProps {
  title: string;
  description?: string;
  points: MonthlyPoint[];
  valueName: string;
  formatValue: (value: number) => string;
  /** A fixed top for the axis, as a rate's 100, so a month at 40٪ never looks full. */
  max?: number;
}

/**
 * One figure per Jalali month (§12: the dashboard adds the server's days up into months), oldest
 * on the right. New members and the renewal rate each get their own chart: a count and a percent
 * on one axis would make one of them unreadable, and two axes would make both misleading. Laid out
 * left to right with `svgText` labels, as `RevenueChart` explains.
 */
export function MonthlyChart({
  title,
  description,
  points,
  valueName,
  formatValue,
  max,
}: MonthlyChartProps) {
  const empty = points.every((point) => point.value === null || point.value === 0);

  return (
    <ChartCard title={title} description={description} empty={empty}>
      <div dir="ltr">
        <BarChart
          responsive
          data={points}
          style={{ width: "100%", height: 240 }}
          margin={{ top: 20 }}
        >
          <CartesianGrid vertical={false} stroke={gridStroke} />
          <XAxis
            dataKey="label"
            reversed
            tick={axisTick}
            tickFormatter={svgText}
            tickLine={false}
            stroke={gridStroke}
          />
          <YAxis
            orientation="right"
            tick={axisTick}
            tickFormatter={(value: number) => svgText(formatValue(value))}
            tickLine={false}
            axisLine={false}
            allowDecimals={false}
            domain={[0, max ?? "auto"]}
            width={48}
          />
          <Tooltip
            cursor={{ fill: "var(--muted)" }}
            content={(props) => {
              const point = hoveredRow<MonthlyPoint>(props);
              return (
                <ChartTooltip
                  active={props.active && point !== undefined}
                  title={point?.label}
                  rows={
                    point === undefined
                      ? []
                      : [
                          {
                            name: valueName,
                            value:
                              point.value === null ? "هنوز معلوم نیست" : formatValue(point.value),
                          },
                          ...(point.details ?? []),
                        ]
                  }
                />
              );
            }}
          />
          <Bar
            dataKey="value"
            name={valueName}
            fill="var(--chart-1)"
            maxBarSize={24}
            radius={[4, 4, 0, 0]}
          >
            <LabelList
              dataKey="value"
              position="top"
              formatter={(value) =>
                value === null || value === undefined ? "" : svgText(formatValue(Number(value)))
              }
              style={{ fill: "var(--foreground)", fontSize: 12 }}
            />
          </Bar>
        </BarChart>
      </div>
    </ChartCard>
  );
}
