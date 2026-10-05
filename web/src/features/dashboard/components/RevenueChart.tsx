import { ChartColumnBig } from "lucide-react";
import { useId } from "react";
import { Bar, BarChart, CartesianGrid, Legend, Tooltip, XAxis, YAxis } from "recharts";

import { formatMoney, formatMoneyShort } from "@/lib/format";

import { revenueSeries, type FinancialDay, type MoneyPoint } from "../figures";
import { axisTick, gridStroke, hoveredRow, svgId, svgText } from "../chartStyle";
import { BarGradient, ChartCard, ChartTooltip } from "./ChartCard";

const revenueName = "درآمد خالص";
const expensesName = "هزینه";

/**
 * Net revenue beside expenses, day by day (§12 *Financial report*), or by Jalali month over a long
 * range. Two bars side by side rather than one axis each: both are Toman, so one scale serves.
 *
 * Time runs right to left, the way the page reads: the oldest day is on the right (decided with
 * the developer, 1405/07/12). `reversed` turns the axis, and the amounts sit on the right too.
 *
 * The chart itself is laid out left to right (`dir="ltr"`): SVG reads `text-anchor` against the
 * text's direction, so in the page's right-to-left every tick would slide over the bars. Its
 * labels go through `svgText` to read right to left again.
 */
export function RevenueChart({ days }: { days: FinancialDay[] }) {
  const points = revenueSeries(days);
  const empty = points.every((point) => point.revenue === 0 && point.expenses === 0);
  const byMonth = points.length > 0 && points.length < days.length;
  const reactId = useId();
  const revenueFill = svgId(reactId, "revenue");
  const expensesFill = svgId(reactId, "expenses");

  return (
    <ChartCard
      title="درآمد و هزینه"
      description={byMonth ? "به تفکیک ماه، به تومان" : "روزبه‌روز، به تومان"}
      icon={ChartColumnBig}
      tone="blue"
      empty={empty}
      className="lg:col-span-2"
    >
      <div dir="ltr">
        <BarChart responsive data={points} style={{ width: "100%", height: 280 }} barGap={2}>
          <BarGradient id={revenueFill} color="var(--chart-1)" />
          <BarGradient id={expensesFill} color="var(--chart-2)" />
          <CartesianGrid vertical={false} strokeDasharray="4 4" stroke={gridStroke} />
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
            tickFormatter={(value: number) => svgText(formatMoneyShort(value))}
            tickLine={false}
            axisLine={false}
            width={72}
          />
          <Tooltip
            cursor={{ fill: "var(--muted)" }}
            content={(props) => {
              const point = hoveredRow<MoneyPoint>(props);
              return (
                <ChartTooltip
                  active={props.active && point !== undefined}
                  title={point?.title}
                  rows={
                    point === undefined
                      ? []
                      : [
                          {
                            name: revenueName,
                            value: formatMoney(point.revenue),
                            color: "var(--chart-1)",
                          },
                          {
                            name: expensesName,
                            value: formatMoney(point.expenses),
                            color: "var(--chart-2)",
                          },
                        ]
                  }
                />
              );
            }}
          />
          {/* The key reads right to left like the page, and its words stay in text ink: the
            swatch beside each one carries the colour. */}
          <Legend
            iconType="square"
            wrapperStyle={{ direction: "rtl", fontSize: 13 }}
            formatter={(value: string) => <span className="text-foreground">{value}</span>}
          />
          <Bar
            dataKey="revenue"
            name={revenueName}
            fill={`url(#${revenueFill})`}
            maxBarSize={24}
            radius={[6, 6, 0, 0]}
          />
          <Bar
            dataKey="expenses"
            name={expensesName}
            fill={`url(#${expensesFill})`}
            maxBarSize={24}
            radius={[6, 6, 0, 0]}
          />
        </BarChart>
      </div>
    </ChartCard>
  );
}
