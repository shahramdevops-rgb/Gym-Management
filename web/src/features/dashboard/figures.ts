import { jalaliMonthNames, jalaliPartsOf, toPersianDigits } from "@/lib/format";

/**
 * How much a figure moved since the range before, in percent: 120 after 100 is 20. Null when the
 * range before had nothing or less than nothing (a loss), where a percent says nothing true: from
 * 0 every rise is infinite, and from −100 to 50 is not "−150٪". The card then shows the figure
 * before instead (decided with the developer, 1405/07/12).
 */
export function percentChange(current: number | string, previous: number | string): number | null {
  const now = Number(current);
  const before = Number(previous);
  if (!Number.isFinite(now) || !Number.isFinite(before) || before <= 0) {
    return null;
  }

  return ((now - before) / before) * 100;
}

/** Whether a figure that can go either way (a profit) came out above or below zero; at zero, neither. */
export type Outcome = "gain" | "loss" | undefined;

/**
 * A profit is shown green and a loss red, with its minus sign (decided with the developer,
 * 1405/07/14). Zero, or an amount that is not a number, is neither.
 */
export function outcomeOf(value: number | string): Outcome {
  const amount = Number(value);
  return amount > 0 ? "gain" : amount < 0 ? "loss" : undefined;
}

/**
 * The share of ended plans that were renewed, in percent (§12 *Operational reports*): renewed ÷
 * (ended − waiting). Plans still inside their 30 days are left out, or the last month would always
 * look worse than it is. Null when no plan has had its full chance yet.
 */
export function renewalRate(renewed: number, ended: number, waiting: number): number | null {
  const decided = ended - waiting;

  return decided > 0 ? (renewed / decided) * 100 : null;
}

/**
 * The share of new people's single visits that became a plan within 30 days, in percent (§12
 * *Operational reports*, decided with the developer, 1405/07/14): converted ÷ (trials − waiting),
 * the renewal rate's own rule. Null when nobody has had their full 30 days yet.
 */
export function conversionRate(converted: number, trials: number, waiting: number): number | null {
  return renewalRate(converted, trials, waiting);
}

/** The hours shown when nobody came at all, so the empty table still has the gym's day. */
const quietDay = { first: 6, last: 23 };

/**
 * The hours worth a column in the weekday × hour table: from the earliest hour anyone came on any
 * weekday to the latest. The gym has no fixed opening hours in the system (§0), and twenty-four
 * columns of mostly nothing would push the busy ones off a laptop screen.
 */
export function busyHours(rows: { hours: (number | string)[] }[]): number[] {
  const used = rows.flatMap((row) =>
    row.hours.flatMap((count, hour) => (Number(count) > 0 ? [hour] : [])),
  );
  const first = used.length === 0 ? quietDay.first : Math.min(...used);
  const last = used.length === 0 ? quietDay.last : Math.max(...used);

  return Array.from({ length: last - first + 1 }, (_, index) => first + index);
}

/** One day of the financial report, as the API sends it. */
export interface FinancialDay {
  date: string;
  revenue: number | string;
  expenses: number | string;
}

/** One bar pair of the revenue chart: a day, or a Jalali month over a long range. */
export interface MoneyPoint {
  key: string;
  /** The axis label: `۷/۱۲` for a day, «مهر ۱۴۰۵» for a month. */
  label: string;
  /** The tooltip's title: the full Jalali date of a day, or the month. */
  title: string;
  revenue: number;
  expenses: number;
}

/**
 * Past this many days the revenue chart adds the days up into Jalali months: a year of daily bars
 * is too thin to read, and two months of them are still a working-day picture.
 */
export const dailyChartMaxDays = 62;

/** The financial report's days as the revenue chart draws them, oldest first. */
export function revenueSeries(days: FinancialDay[]): MoneyPoint[] {
  const points = new Map<string, MoneyPoint>();
  const byMonth = days.length > dailyChartMaxDays;

  for (const day of days) {
    const parts = jalaliPartsOf(day.date);
    if (parts === null) {
      continue;
    }

    const month = String(parts.month).padStart(2, "0");
    const monthName = `${jalaliMonthNames[parts.month - 1]} ${toPersianDigits(parts.year)}`;
    const key = byMonth ? `${parts.year}/${month}` : day.date;
    const point = points.get(key) ?? {
      key,
      label: byMonth ? monthName : toPersianDigits(`${parts.month}/${parts.day}`),
      title: byMonth ? monthName : `${toPersianDigits(parts.day)} ${monthName}`,
      revenue: 0,
      expenses: 0,
    };

    point.revenue += Number(day.revenue);
    point.expenses += Number(day.expenses);
    points.set(key, point);
  }

  return [...points.values()].sort((a, b) => a.key.localeCompare(b.key));
}

/** One day of the members report, as the API sends it. */
export interface MembersDay {
  date: string;
  ended: number | string;
  renewed: number | string;
  waiting: number | string;
  newMembers: number | string;
}

/** One Jalali month of the members report: the days added up, as the dashboard charts them. */
export interface MembersMonth {
  /** `۱۴۰۵/۰۷`, sortable and unique, for the chart's key. */
  key: string;
  /** «مهر ۱۴۰۵». */
  label: string;
  ended: number;
  renewed: number;
  waiting: number;
  newMembers: number;
  /** Renewed ÷ (ended − waiting) of the month, or null when nothing was decided. */
  renewalRate: number | null;
}

/**
 * The members report's days added up into Jalali months, oldest first (§12: the server keeps to
 * Gregorian dates, the dashboard groups by the calendar the Owner thinks in). A range that starts
 * or ends inside a month gives that month only its days in the range.
 */
export function byJalaliMonth(days: MembersDay[]): MembersMonth[] {
  const months = new Map<string, MembersMonth>();

  for (const day of days) {
    const parts = jalaliPartsOf(day.date);
    if (parts === null) {
      continue;
    }

    const key = `${parts.year}/${String(parts.month).padStart(2, "0")}`;
    const month = months.get(key) ?? {
      key,
      label: `${jalaliMonthNames[parts.month - 1]} ${toPersianDigits(parts.year)}`,
      ended: 0,
      renewed: 0,
      waiting: 0,
      newMembers: 0,
      renewalRate: null,
    };

    month.ended += Number(day.ended);
    month.renewed += Number(day.renewed);
    month.waiting += Number(day.waiting);
    month.newMembers += Number(day.newMembers);
    months.set(key, month);
  }

  return [...months.values()]
    .sort((a, b) => a.key.localeCompare(b.key))
    .map((month) => ({
      ...month,
      renewalRate: renewalRate(month.renewed, month.ended, month.waiting),
    }));
}
