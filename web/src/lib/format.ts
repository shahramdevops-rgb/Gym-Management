/**
 * Display formatting for the Persian UI.
 *
 * The API speaks Gregorian ISO dates, UTC timestamps and English digits; the screen speaks
 * Jalali dates and Persian digits. That translation happens here and nowhere else, so a
 * component never calls toLocaleString() with its own set of options.
 */

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
