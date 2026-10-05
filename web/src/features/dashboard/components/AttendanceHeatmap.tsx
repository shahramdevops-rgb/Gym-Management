import { Flame } from "lucide-react";

import { formatNumber, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { AttendanceReport } from "../api";
import { busyHours } from "../figures";
import { ChartCard } from "./ChartCard";

type Weekday = AttendanceReport["byWeekday"][number]["weekday"];

const weekdayLabels: Record<Weekday, string> = {
  Saturday: "شنبه",
  Sunday: "یکشنبه",
  Monday: "دوشنبه",
  Tuesday: "سه‌شنبه",
  Wednesday: "چهارشنبه",
  Thursday: "پنجشنبه",
  Friday: "جمعه",
};

/**
 * Members' check-ins by weekday and hour (§12 *Operational reports*), Saturday first, shaded in
 * one hue from nothing to the busiest hour. A table rather than a chart: Recharts has no heat map,
 * and a table reads cell by cell to a screen reader as it is.
 */
export function AttendanceHeatmap({ report }: { report: AttendanceReport }) {
  const hours = busyHours(report.byWeekday);
  const busiest = Math.max(
    0,
    ...report.byWeekday.flatMap((row) => row.hours.map((count) => Number(count))),
  );

  return (
    <ChartCard
      title="ورود اعضا به تفکیک روز هفته و ساعت"
      description="هر خانه: تعداد ورودها در آن ساعت. مهمان‌ها و ورودهای لغوشده حساب نمی‌شوند."
      icon={Flame}
      tone="sky"
      className="lg:col-span-2"
    >
      <div className="overflow-x-auto">
        <table className="w-full border-separate border-spacing-0.5 text-xs tabular-nums">
          <thead>
            <tr>
              <th scope="col" className="sr-only">
                روز
              </th>
              {hours.map((hour) => (
                <th key={hour} scope="col" className="font-normal text-muted-foreground">
                  {toPersianDigits(hour)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {report.byWeekday.map((row) => (
              <tr key={row.weekday}>
                <th scope="row" className="pe-2 text-start font-normal whitespace-nowrap">
                  {weekdayLabels[row.weekday]}
                </th>
                {hours.map((hour) => {
                  const count = Number(row.hours[hour] ?? 0);
                  // At least a fifth of the hue for any visit, so one visit is not mistaken for none.
                  const share = busiest === 0 || count === 0 ? 0 : 20 + (count / busiest) * 80;
                  return (
                    <td
                      key={hour}
                      title={`${weekdayLabels[row.weekday]}، ساعت ${toPersianDigits(hour)}: ${formatNumber(count)} ورود`}
                      className={cn(
                        "h-8 min-w-8 rounded-md text-center",
                        count === 0 && "bg-muted/60 text-muted-foreground",
                        share > 55 && "text-white",
                      )}
                      style={
                        count === 0
                          ? undefined
                          : {
                              background: `color-mix(in srgb, var(--chart-1) ${share}%, transparent)`,
                            }
                      }
                    >
                      {count === 0 ? "" : formatNumber(count)}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </ChartCard>
  );
}
