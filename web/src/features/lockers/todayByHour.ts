import { gymTimeZone } from "@/lib/format";

/** One hour of the chart: today's check-ins and the same hour's usual count. */
export interface HourCount {
  /** 0 to 23, in the gym's time zone. */
  hour: number;
  today: number;
  average: number;
}

const hourFormatter = new Intl.DateTimeFormat("en-US", {
  timeZone: gymTimeZone,
  hour: "numeric",
  hourCycle: "h23",
});

/** The hour a moment falls in on the gym's clock (0 to 23), whatever zone the browser is set to. */
export function gymHour(moment: Date): number {
  return Number(hourFormatter.format(moment));
}

/**
 * The hours worth drawing: from the first to the last that has anything, today or on average, with
 * every hour between them kept so the gaps show. The night the gym is shut stays off the chart.
 * Empty when nothing has happened at all, today or in the weeks the average covers.
 */
export function visibleHours(hours: readonly HourCount[]): HourCount[] {
  const busy = hours.filter((row) => row.today > 0 || row.average > 0);
  if (busy.length === 0) {
    return [];
  }

  const first = Math.min(...busy.map((row) => row.hour));
  const last = Math.max(...busy.map((row) => row.hour));

  return hours.filter((row) => row.hour >= first && row.hour <= last);
}
