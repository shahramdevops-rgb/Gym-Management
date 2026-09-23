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

import { normalizeMoney } from "./money";
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

// ---- Money ----
//
// Every amount on screen comes through here (docs/BUSINESS_RULES.md §13). Toman amounts run to
// seven and eight digits, where ۵۰۰٬۰۰۰ and ۵٬۰۰۰٬۰۰۰ look alike at a glance, so an amount is
// never formatted by the screen that shows it.
//
// These work on the decimal *string*, not on a number: `Intl.NumberFormat` would need a float
// first, and a float cannot hold every amount the API accepts. `formatNumber` above is still
// the right tool for a count of sessions or members — just never for money.

const moneyPattern = /^(\d+)(?:\.(\d*))?$/;

/** Digits in groups of three, Persian, with ٬ between groups: `1500000` → `۱٬۵۰۰٬۰۰۰`. */
function groupThousands(whole: string): string {
  return toPersianDigits(whole.replace(/\B(?=(\d{3})+(?!\d))/g, "٬"));
}

/**
 * A plain decimal string exactly as it stands, in Persian: `1500000.5` → `۱٬۵۰۰٬۰۰۰٫۵`. No
 * unit, and nothing trimmed or added — a half-typed `1500000.` stays `۱٬۵۰۰٬۰۰۰٫`, which is
 * what a money box needs while somebody is still typing into it. Empty for anything that is
 * not a number.
 */
export function formatMoneyDigits(digits: string): string {
  if (digits === "") {
    return "";
  }

  const parts = moneyPattern.exec(digits);
  if (parts === null) {
    return "";
  }

  const [, whole = "", fraction] = parts;

  return fraction === undefined
    ? groupThousands(whole)
    : `${groupThousands(whole)}٫${toPersianDigits(fraction)}`;
}

/**
 * An amount of money as it is shown: `۱٬۲۵۰٬۰۰۰ تومان`. The unit is Toman
 * (docs/BUSINESS_RULES.md §0) and the value is shown exactly as stored, never divided by
 * anything — guessing a sub-unit divisor here would be a silent factor-of-ten bug.
 *
 * A fraction of nothing is dropped, so the `numeric(18,2)` the API sends back as `1500000.00`
 * reads as `۱٬۵۰۰٬۰۰۰ تومان` rather than trailing two zeros nobody typed.
 */
export function formatMoney(value: string | number | null | undefined): string {
  if (value === null || value === undefined || value === "") {
    return emptyValue;
  }

  const amount = normalizeMoney(String(value));
  const parts = moneyPattern.exec(amount);
  if (parts === null) {
    return emptyValue;
  }

  const [, whole = "", fraction = ""] = parts;
  const withoutEmptyFraction = /^0*$/.test(fraction) ? whole : `${whole}.${fraction}`;

  return `${formatMoneyDigits(withoutEmptyFraction)} تومان`;
}

// The amount written out in words, which is the actual protection: a run of zeros can be
// miscounted, «پانصد هزار» cannot. Hand-written rather than a package, because the rules fit on
// one screen and a dependency here would still have to be checked digit by digit.

const onesWords = ["", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه"];

// Ten to nineteen are their own words in Persian, as they are in English.
const teenWords = [
  "ده",
  "یازده",
  "دوازده",
  "سیزده",
  "چهارده",
  "پانزده",
  "شانزده",
  "هفده",
  "هجده",
  "نوزده",
];

const tensWords = ["", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"];

const hundredsWords = [
  "",
  "صد",
  "دویست",
  "سیصد",
  "چهارصد",
  "پانصد",
  "ششصد",
  "هفتصد",
  "هشتصد",
  "نهصد",
];

/**
 * The scale of each group of three digits, smallest first. Six groups cover eighteen digits,
 * and the largest amount the API accepts has sixteen.
 */
const scaleWords = ["", "هزار", "میلیون", "میلیارد", "هزار میلیارد", "میلیون میلیارد"];

const and = " و ";

/** A number from 1 to 999 in words: `115` → «صد و پانزده». */
function threeDigitsInWords(value: number): string {
  const parts: string[] = [];
  const hundreds = Math.floor(value / 100);
  const rest = value % 100;

  if (hundreds > 0) {
    parts.push(hundredsWords[hundreds]!);
  }
  if (rest >= 10 && rest <= 19) {
    parts.push(teenWords[rest - 10]!);
  } else {
    const tens = Math.floor(rest / 10);
    const ones = rest % 10;
    if (tens > 0) {
      parts.push(tensWords[tens]!);
    }
    if (ones > 0) {
      parts.push(onesWords[ones]!);
    }
  }

  return parts.join(and);
}

/** The whole part, split into groups of three from the right and named by its scale. */
function wholeInWords(whole: string): string | null {
  const digits = whole.replace(/^0+(?=\d)/, "");
  const groups: number[] = [];

  for (let end = digits.length; end > 0; end -= 3) {
    groups.push(Number(digits.slice(Math.max(0, end - 3), end)));
  }

  if (groups.length > scaleWords.length) {
    return null;
  }

  const parts: string[] = [];

  // Largest scale first, which is the order the words are said in.
  for (let scale = groups.length - 1; scale >= 0; scale -= 1) {
    const group = groups[scale]!;
    if (group === 0) {
      continue;
    }

    const scaleWord = scaleWords[scale]!;
    if (scaleWord === "") {
      parts.push(threeDigitsInWords(group));
    } else if (group === 1 && scaleWord.startsWith("هزار")) {
      // «هزار تومان», never «یک هزار تومان» — but «یک میلیون» keeps its یک.
      parts.push(scaleWord);
    } else {
      parts.push(`${threeDigitsInWords(group)} ${scaleWord}`);
    }
  }

  return parts.join(and);
}

/**
 * The amount written out: `500000` → «پانصد هزار تومان». This goes under every money box, and
 * it is the check — nobody miscounts a word (docs/BUSINESS_RULES.md §13).
 *
 * Empty for anything that is not an amount, including a half-typed one and a fraction finer
 * than the two decimals money has, so the line simply disappears rather than saying something
 * that is not true.
 */
export function amountInPersianWords(value: string | number | null | undefined): string {
  if (value === null || value === undefined || value === "") {
    return "";
  }

  const parts = moneyPattern.exec(normalizeMoney(String(value)));
  if (parts === null) {
    return "";
  }

  const [, whole = "", fraction = ""] = parts;
  if (fraction.length > 2) {
    return "";
  }

  const words: string[] = [];

  const wholeWords = wholeInWords(whole);
  if (wholeWords === null) {
    return "";
  }
  if (wholeWords !== "") {
    words.push(wholeWords);
  }

  // Stored as numeric(18,2), so a fraction is always some number of hundredths: `.5` is fifty
  // of them, `.05` is five.
  const hundredths = fraction === "" ? 0 : Number(fraction.padEnd(2, "0"));
  if (hundredths > 0) {
    words.push(`${threeDigitsInWords(hundredths)} صدم`);
  }

  return words.length === 0 ? "صفر تومان" : `${words.join(and)} تومان`;
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

  return match === null ? null : jalaliToIso(Number(match[1]), Number(match[2]), Number(match[3]));
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
