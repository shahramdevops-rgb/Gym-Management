import { useTodayByHour } from "@/features/attendance/api";
import { formatNumber, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import { gymHour, visibleHours, type HourCount } from "../todayByHour";

/** One hour's column in the drawing's own units; the drawing scales to the width it is given. */
const slot = 36;
const barWidth = 20;
/** Room above the bars for a count, the bars themselves, and room below them for the hour. */
const top = 16;
const plotHeight = 96;
const bottom = 20;
const height = top + plotHeight + bottom;

/** How many weeks back the average reaches: `TodayByHourHandler.WeeksAveraged`. */
const weeksAveraged = 4;

const weekdayFormatter =new Intl.DateTimeFormat("fa-IR", { timeZone: "UTC", weekday: "long" });

/** The weekday a business date (`2026-09-30`) falls on, in Persian: «چهارشنبه». */
function weekdayOf(date: string): string {
  return weekdayFormatter.format(new Date(`${date}T00:00:00Z`));
}

/** «ساعت ۱۸ تا ۱۹» */
function hourRange(hour: number): string {
  return `ساعت ${toPersianDigits(hour)} تا ${toPersianDigits(hour + 1)}`;
}

/**
 * The chart under the reserve places (BUSINESS_RULES.md §6 *Today by hour*): how many checked in
 * during each hour of today, beside the same hour's average on the same weekday over the previous
 * 4 weeks, so the desk and the Owner see whether today is busier or quieter than usual.
 *
 * It is extra information on the desk's screen, so it never gets in the way: while it loads nothing
 * is drawn, and if it fails one quiet line says so and the map goes on working.
 */
export function TodayByHourChart({ now }: { now: Date }) {
  const byHour = useTodayByHour();

  if (byHour.isPending) {
    return null;
  }

  return (
    <section
      aria-label="ورود امروز ساعت به ساعت"
      className="space-y-3 rounded-xl border bg-card px-4 py-3"
    >
      {byHour.isError ? (
        <p className="text-sm text-muted-foreground">نمودار ورود امروز بارگذاری نشد.</p>
      ) : (
        <HourBars
          date={byHour.data.date}
          daysAveraged={byHour.data.daysAveraged}
          hours={byHour.data.hours}
          currentHour={gymHour(now)}
        />
      )}
    </section>
  );
}

interface HourBarsProps {
  /** Today, as the API saw it: the day the counts are for. */
  date: string;
  /** How many of the previous 4 same weekdays had check-ins and went into the average. */
  daysAveraged: number;
  hours: HourCount[];
  /** The hour it is now on the gym's clock, marked on the chart. */
  currentHour: number;
}

/**
 * The drawing itself, in plain SVG (no chart package is listed). Each hour is a bar for today and a
 * dashed line across it at the usual count. Time runs from right to left, as it does along the
 * long-stay bar and as a Persian reader reads. The hour it is now is marked; later hours have no bar
 * yet, only their usual count, which shows what is still to come.
 *
 * The SVG is hidden from a screen reader, which reads the same numbers from a table instead.
 */
function HourBars({ date, daysAveraged, hours, currentHour }: HourBarsProps) {
  const shown = visibleHours(hours);
  const hasAverage = daysAveraged > 0;
  // A closed day is left out of the average (§6), which the label says when it happened.
  const weekday = weekdayOf(date);
  const averageLabel = !hasAverage
    ? "هنوز میانگینی از هفته‌های گذشته نیست"
    : daysAveraged === weeksAveraged
      ? `میانگین ${weekday}‌های ${toPersianDigits(weeksAveraged)} هفتهٔ گذشته`
      : `میانگین ${toPersianDigits(daysAveraged)} ${weekday} باز در ${toPersianDigits(weeksAveraged)} هفتهٔ گذشته`;

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
        <h3 className="text-sm font-bold">ورود امروز ساعت به ساعت</h3>
        <ul aria-hidden className="flex flex-wrap items-center gap-x-5 gap-y-1 text-xs text-muted-foreground">
          <li className="flex items-center gap-2">
            <span className="inline-block size-3 rounded-[2px] bg-success" />
            امروز
          </li>
          <li className="flex items-center gap-2">
            <span className="inline-block w-4 border-t-2 border-dashed border-muted-foreground" />
            {averageLabel}
          </li>
        </ul>
      </div>

      {shown.length === 0 ? (
        <p className="text-sm text-muted-foreground">امروز هنوز ورودی ثبت نشده است.</p>
      ) : (
        <>
          <Bars hours={shown} currentHour={currentHour} hasAverage={hasAverage} />
          <table className="sr-only">
            <caption>ورود امروز ساعت به ساعت، و {averageLabel}</caption>
            <thead>
              <tr>
                <th scope="col">ساعت</th>
                <th scope="col">امروز</th>
                {hasAverage && <th scope="col">میانگین</th>}
              </tr>
            </thead>
            <tbody>
              {shown.map((row) => (
                <tr key={row.hour}>
                  <th scope="row">{hourRange(row.hour)}</th>
                  <td>{formatNumber(row.today)}</td>
                  {hasAverage && <td>{formatNumber(row.average)}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  );
}

function Bars({
  hours,
  currentHour,
  hasAverage,
}: {
  hours: HourCount[];
  currentHour: number;
  hasAverage: boolean;
}) {
  const width = hours.length * slot;
  const highest = Math.max(1, ...hours.map((row) => Math.max(row.today, row.average)));
  const heightOf = (value: number) => (value / highest) * plotHeight;
  const baseline = top + plotHeight;

  return (
    <svg
      aria-hidden
      data-testid="today-by-hour-bars"
      viewBox={`0 0 ${width} ${height}`}
      className="h-36 w-full"
    >
      <line x1={0} x2={width} y1={baseline} y2={baseline} strokeWidth={1} className="stroke-border" />
      {hours.map((row, index) => {
        // The first hour on the right: right to left, as time runs along the long-stay bar.
        const x = width - (index + 1) * slot;
        const centre = x + slot / 2;
        const isNow = row.hour === currentHour;
        const barHeight = heightOf(row.today);
        const averageY = baseline - heightOf(row.average);
        const title =
          `${hourRange(row.hour)}: امروز ${formatNumber(row.today)}` +
          (hasAverage ? `، میانگین ${formatNumber(row.average)}` : "");

        return (
          <g key={row.hour} data-hour={row.hour} data-now={isNow || undefined}>
            <title>{title}</title>
            {/* The whole column answers to the mouse, so an hour with no bar still has its title. */}
            <rect
              x={x}
              y={0}
              width={slot}
              height={height}
              className={isNow ? "fill-foreground/5" : "fill-transparent"}
            />
            {row.today > 0 && (
              <>
                <rect
                  x={centre - barWidth / 2}
                  y={baseline - barHeight}
                  width={barWidth}
                  height={barHeight}
                  rx={3}
                  className={isNow ? "fill-success" : "fill-success/70"}
                />
                <text
                  x={centre}
                  y={baseline - barHeight - 4}
                  textAnchor="middle"
                  className="fill-foreground text-[10px] font-bold"
                >
                  {toPersianDigits(row.today)}
                </text>
              </>
            )}
            {hasAverage && row.average > 0 && (
              <line
                x1={centre - slot / 2 + 4}
                x2={centre + slot / 2 - 4}
                y1={averageY}
                y2={averageY}
                strokeWidth={2}
                strokeDasharray="4 3"
                className="stroke-muted-foreground"
              />
            )}
            <text
              x={centre}
              y={height - 5}
              textAnchor="middle"
              className={cn(
                "text-[10px]",
                isNow ? "fill-foreground font-bold" : "fill-muted-foreground",
              )}
            >
              {toPersianDigits(row.hour)}
            </text>
          </g>
        );
      })}
    </svg>
  );
}
