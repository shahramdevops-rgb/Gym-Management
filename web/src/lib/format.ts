/**
 * Translation between what the API speaks and what the Persian UI speaks.
 *
 * The API speaks Gregorian ISO dates, UTC timestamps and English digits; the screen speaks
 * Jalali dates and Persian digits. That translation happens here and nowhere else, so a
 * component never calls toLocaleString() with its own set of options.
 *
 * It goes both ways: `formatDate` and friends are for showing what was stored, and `toIsoDate`
 * and friends are for a date that was typed into a Jalali calendar and has to be sent back as
 * Gregorian (docs/BUSINESS_RULES.md §13).
 */

import {
  getDate as getJalaliDay,
  getMonth as getJalaliMonthIndex,
  getYear as getJalaliYear,
  newDate as newJalaliDate,
} from "date-fns-jalali";

import { normalizeDigits } from "./normalize";

/** Shown instead of a date or amount that is missing or unparseable. */
export const emptyValue = "—";

/** docs/BUSINESS_RULES.md section 0: every business date is "today" in the gym's time zone. */
export const gymTimeZone = "Asia/Tehran";

const locale = "fa-IR";

const numberFormatter = new Intl.NumberFormat(locale);

// A business date (`2026-09-17`) is a calendar day, not a moment. It is parsed as UTC midnight
// and formatted in UTC, so the day it names can never shift, whatever the gym's offset.
const dateFormatter = new Intl.DateTimeFormat(locale, {
  calendar: "persian",
  timeZone: "UTC",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

const dateTimeFormatter = new Intl.DateTimeFormat(locale, {
  calendar: "persian",
  timeZone: gymTimeZone,
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
});

/** Rewrites English digits as Persian ones, leaving everything else alone. */
export function toPersianDigits(value: string | number): string {
  return String(value).replace(/[0-9]/g, (digit) => String.fromCodePoint(0x06f0 + Number(digit)));
}

/** A number with Persian digits and Persian thousands separators. */
export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return emptyValue;
  }

  return numberFormatter.format(value);
}

/**
 * An amount of money. The unit is Toman (docs/BUSINESS_RULES.md section 0); the value is
 * formatted exactly as stored, because whether Toman are stored whole or in a sub-unit is a
 * Phase 4 decision and guessing a divisor here would be a silent factor-of-ten bug.
 */
export function formatMoney(value: number | null | undefined): string {
  const amount = formatNumber(value);

  return amount === emptyValue ? emptyValue : `${amount} تومان`;
}

const isoDatePattern = /^\d{4}-\d{2}-\d{2}$/;

function parseTimestamp(value: string | null | undefined): Date | null {
  if (value === null || value === undefined || value === "") {
    return null;
  }

  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? null : date;
}

/** An ISO business date (`2026-09-17`) as a Jalali date. Anything else is shown as missing. */
export function formatDate(value: string | null | undefined): string {
  if (value === null || value === undefined || !isoDatePattern.test(value)) {
    return emptyValue;
  }

  // Date.parse treats a bare ISO date as UTC midnight, which is what dateFormatter expects.
  const date = parseTimestamp(value);

  return date === null ? emptyValue : dateFormatter.format(date);
}

/** A UTC timestamp as a Jalali date and a time in the gym's time zone. */
export function formatDateTime(value: string | null | undefined): string {
  const date = parseTimestamp(value);

  return date === null ? emptyValue : dateTimeFormatter.format(date);
}

/**
 * A stored E.164 Iranian mobile (`+989121234567`) the way people write it: `۰۹۱۲ ۱۲۳ ۴۵۶۷`.
 * Anything else is shown as it is, with Persian digits.
 *
 * Show the result inside an element with `dir="ltr"`: in a right-to-left paragraph the three
 * space-separated groups would otherwise be laid out right to left, as `۴۵۶۷ ۱۲۳ ۰۹۱۲`.
 */
export function formatPhone(value: string | null | undefined): string {
  if (value === null || value === undefined || value === "") {
    return emptyValue;
  }

  const iranianMobile = /^\+98(9\d{2})(\d{3})(\d{4})$/.exec(value);
  if (iranianMobile === null) {
    return toPersianDigits(value);
  }

  const [, operator, middle, last] = iranianMobile;

  return toPersianDigits(`0${operator} ${middle} ${last}`);
}

// ---- Jalali input: the direction the screen sends back ----
//
// The formatters above turn what the API stores into what the screen shows. A date that is
// *typed* has to travel the other way, and that conversion is arithmetic rather than formatting,
// so it uses date-fns-jalali rather than Intl.

/**
 * Midday, never midnight. `newDate` and `new Date(y, m, d)` build a local-time value, and Iran
 * moved its clocks at midnight in every year it kept daylight saving (1991–2005, 2008–2022) —
 * exactly the years birth dates fall in. A date built at 00:00 on one of those nights lands on
 * the day before. No time zone has ever skipped noon.
 */
const midday = 12;

/** A typed Jalali date: four-or-three-digit year, then month and day, `/` or `-`, zeros optional. */
const jalaliInputPattern = /^(\d{3,4})[/-](\d{1,2})[/-](\d{1,2})$/;

function pad(value: number, length = 2): string {
  return String(value).padStart(length, "0");
}

/** A Jalali year, month (1-12) and day as the ISO Gregorian date the API stores. */
export function jalaliToIso(year: number, month: number, day: number): string | null {
  if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(day)) {
    return null;
  }

  if (month < 1 || month > 12 || day < 1 || day > 31) {
    return null;
  }

  const date = newJalaliDate(year, month - 1, day, midday);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  // newDate rolls an impossible day into the next month (۱۴۰۴/۱۲/۳۰ becomes ۱۴۰۵/۰۱/۰۱) instead
  // of refusing it. Converting back is what proves the day the user typed actually exists.
  const real =
    getJalaliYear(date) === year &&
    getJalaliMonthIndex(date) === month - 1 &&
    getJalaliDay(date) === day;

  return real
    ? `${pad(date.getFullYear(), 4)}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
    : null;
}

/**
 * A typed Jalali date as the ISO Gregorian date the API stores: `۱۳۷۰/۰۵/۱۲` → `1991-08-03`.
 * Persian, Arabic and English digits, `/` or `-`, leading zeros optional. Anything that is not
 * one whole, real date is null — a half-typed date is not a date.
 */
export function toIsoDate(value: string | null | undefined): string | null {
  if (value === null || value === undefined) {
    return null;
  }

  const match = jalaliInputPattern.exec(normalizeDigits(value).trim());

  return match === null
    ? null
    : jalaliToIso(Number(match[1]), Number(match[2]), Number(match[3]));
}

/** The Jalali year, month (1-12) and day of an ISO business date, for the calendar picker. */
export function jalaliPartsOf(
  value: string | null | undefined,
): { year: number; month: number; day: number } | null {
  if (value === null || value === undefined || !isoDatePattern.test(value)) {
    return null;
  }

  // Sliced rather than split: the pattern has already fixed the widths.
  const date = new Date(
    Number(value.slice(0, 4)),
    Number(value.slice(5, 7)) - 1,
    Number(value.slice(8, 10)),
    midday,
  );

  return {
    year: getJalaliYear(date),
    month: getJalaliMonthIndex(date) + 1,
    day: getJalaliDay(date),
  };
}

/**
 * An ISO business date as the text a Jalali date box shows: `1991-08-03` → `۱۳۷۰/۰۵/۱۲`.
 * Empty for anything that is not an ISO date, so a blank field stays blank.
 *
 * Built from the parts and run through `toPersianDigits` rather than through date-fns-jalali's
 * own `format`, whose default locale decides the digits for itself.
 */
export function toJalaliInput(value: string | null | undefined): string {
  const parts = jalaliPartsOf(value);

  return parts === null
    ? ""
    : toPersianDigits(`${pad(parts.year, 4)}/${pad(parts.month)}/${pad(parts.day)}`);
}

const gymDateParts = new Intl.DateTimeFormat("en-US", {
  timeZone: gymTimeZone,
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

/**
 * Today as the gym sees it (`2026-09-22`): the same definition the API's `IGymCalendar.Today()`
 * uses, so a form and the handler that receives it agree on what day it is even when the
 * receptionist's laptop is set to another zone.
 */
export function gymToday(): string {
  const parts = gymDateParts.formatToParts(new Date());
  const part = (type: string) => parts.find((piece) => piece.type === type)?.value ?? "";

  return `${part("year")}-${part("month")}-${part("day")}`;
}

/**
 * The same date a number of years earlier, clamping 29 February to the 28th when the earlier
 * year is not a leap year — exactly what `DateOnly.AddYears` does on the API side, so the form
 * and the entity draw the 120-year line on the same day.
 */
export function isoYearsAgo(value: string, years: number): string {
  const year = Number(value.slice(0, 4)) - years;
  const month = Number(value.slice(5, 7));
  const day = Number(value.slice(8, 10));
  const isLeap = (candidate: number) =>
    (candidate % 4 === 0 && candidate % 100 !== 0) || candidate % 400 === 0;

  // Shifting whole years can only overflow on one day of the year.
  const clamped = month === 2 && day === 29 && !isLeap(year) ? 28 : day;

  return `${pad(year, 4)}-${pad(month)}-${pad(clamped)}`;
}
