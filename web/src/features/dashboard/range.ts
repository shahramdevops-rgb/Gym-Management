import { isoDaysBefore } from "@/features/history/range";
import { jalaliPartsOf, jalaliToIso } from "@/lib/format";

/**
 * The longest range a report reads (BUSINESS_RULES.md §12 *Financial report*): a Jalali leap
 * year. The API enforces the same number (`ReportRange.MaxDays`); this copy only lets the page say
 * so before asking.
 */
export const maxReportDays = 366;

/**
 * The ranges the dashboard offers in one press (§12 *Dashboard*). The ones named "this" run from
 * their first day to today, not to their last day: the days after today hold nothing yet, and the
 * comparison with the range before is only fair over the same number of days.
 */
export type RangePreset = "today" | "week" | "month" | "lastMonth" | "year";

export const rangePresets: RangePreset[] = ["today", "week", "month", "lastMonth", "year"];

export const rangePresetLabels: Record<RangePreset, string> = {
  today: "امروز",
  week: "این هفته",
  month: "این ماه",
  lastMonth: "ماه قبل",
  year: "امسال",
};

/** What the dashboard opens on (decided with the developer, 1405/07/12). */
export const defaultRangePreset: RangePreset = "month";

export interface ReportRange {
  from: string;
  to: string;
}

/** Saturday, which starts the Iranian week, as `Date.getUTCDay()` numbers it. */
const saturday = 6;

/** Days since the last Saturday: 0 on a Saturday, 6 on a Friday. */
function daysSinceSaturday(iso: string): number {
  const weekday = new Date(`${iso}T00:00:00Z`).getUTCDay();

  return (weekday - saturday + 7) % 7;
}

/** The ISO date of a Jalali day the calendar is known to hold (the 1st of a month always is). */
function isoOf(year: number, month: number, day: number): string {
  const iso = jalaliToIso(year, month, day);
  if (iso === null) {
    throw new Error(`No Jalali day ${year}/${month}/${day}`);
  }

  return iso;
}

/** The range a preset covers, given the gym's today (ISO). */
export function presetRange(preset: RangePreset, today: string): ReportRange {
  const parts = jalaliPartsOf(today);
  if (parts === null) {
    throw new Error(`Not an ISO date: ${today}`);
  }

  switch (preset) {
    case "today":
      return { from: today, to: today };
    case "week":
      return { from: isoDaysBefore(today, daysSinceSaturday(today)), to: today };
    case "month":
      return { from: isoOf(parts.year, parts.month, 1), to: today };
    case "lastMonth": {
      // The day before this month's 1st is last month's last day, 29, 30 or 31 alike.
      const firstOfThisMonth = isoOf(parts.year, parts.month, 1);
      const lastMonth =
        parts.month === 1
          ? { year: parts.year - 1, month: 12 }
          : { year: parts.year, month: parts.month - 1 };
      return {
        from: isoOf(lastMonth.year, lastMonth.month, 1),
        to: isoDaysBefore(firstOfThisMonth, 1),
      };
    }
    case "year":
      return { from: isoOf(parts.year, 1, 1), to: today };
  }
}

/** The preset a range is, so its button reads pressed; undefined for a range of one's own. */
export function presetOf(range: ReportRange, today: string): RangePreset | undefined {
  return rangePresets.find((preset) => {
    const candidate = presetRange(preset, today);
    return candidate.from === range.from && candidate.to === range.to;
  });
}

/** How many days a range covers, both ends included. */
export function rangeDays({ from, to }: ReportRange): number {
  const day = 24 * 60 * 60 * 1000;

  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / day) + 1;
}

/**
 * Why the API would refuse a range, as its error code (`Reports.*`), or undefined when it would
 * take it. The page shows the code's message instead of asking.
 */
export function rangeError(range: Partial<ReportRange>): string | undefined {
  if (range.from === undefined || range.to === undefined) {
    return "Reports.DateRangeRequired";
  }
  if (range.from > range.to) {
    return "Reports.InvalidDateRange";
  }
  if (rangeDays({ from: range.from, to: range.to }) > maxReportDays) {
    return "Reports.RangeTooLong";
  }

  return undefined;
}
