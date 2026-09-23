/**
 * A money amount on its way *in*: from what somebody typed to the plain decimal string the API
 * reads. The other direction — what an amount looks like on screen — lives in `format.ts`
 * (`formatMoney`, `amountInPersianWords`).
 *
 * Everything here is string in, string out. A JavaScript number cannot hold every amount the
 * API accepts (sixteen digits before the point, two after), and rounding a price is never
 * acceptable, so no money value is ever parsed as a float (docs/BUSINESS_RULES.md §13).
 *
 * This is the one place that knows how a typed amount is spelled. Plans and payments each had
 * their own copy (`normalizePrice`, `normalizeAmount`), deliberately, until the cardio charge
 * (roadmap 5.7) became the third caller.
 */

import { normalizeDigits } from "./normalize";

/**
 * An amount as typed (`۹۰۰٬۰۰۰`, `900,000.50`) as the plain decimal string the API reads
 * (`900000.50`): English digits, no thousands separators, `.` as the decimal point.
 *
 * Anything that is not a digit or a separator is left alone rather than stripped, so a caller
 * that validates the result can tell "۹۰۰ تومان" apart from a number and say so.
 */
export function normalizeMoney(text: string): string {
  return normalizeDigits(text)
    .replace(/[\s,٬]/g, "") // spaces, commas and the Persian thousands separator ٬
    .replace(/٫/g, "."); // the Persian decimal separator ٫
}

/**
 * The same, but keeping only what can be part of a number: digits and at most one decimal
 * point. This is what a money box holds *while it is being typed*, where a stray character
 * should simply never appear rather than turn into an error message a keystroke later.
 */
export function moneyDigits(text: string): string {
  const digitsAndPoints = normalizeMoney(text).replace(/[^\d.]/g, "");
  const firstPoint = digitsAndPoints.indexOf(".");

  if (firstPoint === -1) {
    return digitsAndPoints;
  }

  // Every point after the first is dropped, so "1.2.3" settles as "1.23" instead of being
  // refused: a second point is a slip of the finger, not a different amount.
  return `${digitsAndPoints.slice(0, firstPoint)}.${digitsAndPoints.slice(firstPoint + 1).replace(/\./g, "")}`;
}

/**
 * A typed amount for a field that may be left empty (an optional charge): the normalized
 * amount, or null when the box is blank. Null is what the API means by "no amount" — sending
 * `""` or `0` instead would be a different thing entirely.
 */
export function moneyOrNull(text: string): string | null {
  const amount = normalizeMoney(text).trim();

  return amount === "" ? null : amount;
}

/**
 * An amount as a whole number of hundredths (`1500000.5` → `150000050n`), which is what the
 * API's `numeric(18,2)` really is. Null for anything that is not an amount.
 *
 * `bigint`, not `number`: sixteen digits of Toman plus two of fraction is eighteen significant
 * digits, and a double runs out of those at fifteen.
 */
function hundredths(value: string | number): bigint | null {
  const parts = /^(\d+)(?:\.(\d*))?$/.exec(normalizeMoney(String(value)));
  if (parts === null) {
    return null;
  }

  const [, whole = "", fraction = ""] = parts;

  return BigInt(whole + fraction.slice(0, 2).padEnd(2, "0"));
}

/**
 * One amount minus another, exactly — `price - netPaid`, the amount still owed. Both sides and
 * the answer are decimal strings; nothing is ever a float, so no subtraction can end in
 * `0.30000000000000004`. Empty when either side is not an amount.
 */
export function subtractMoney(
  minuend: string | number | null | undefined,
  subtrahend: string | number | null | undefined,
): string {
  const left = minuend === null || minuend === undefined ? null : hundredths(minuend);
  const right = subtrahend === null || subtrahend === undefined ? null : hundredths(subtrahend);
  if (left === null || right === null) {
    return "";
  }

  const difference = left - right;
  const sign = difference < 0n ? "-" : "";
  const digits = (difference < 0n ? -difference : difference).toString().padStart(3, "0");

  return `${sign}${digits.slice(0, -2)}.${digits.slice(-2)}`;
}

/**
 * Whether a stored amount is more than zero — the "does this member owe anything" question,
 * answered without parsing the amount as a float.
 *
 * It takes `number` as well because the API's JSON hands a `decimal` over as a JSON number, so
 * the value has already been through `JSON.parse` by the time any screen sees it. Converting it
 * back to a string here keeps that as the *only* place a money value is a number.
 */
export function isPositiveMoney(value: string | number | null | undefined): boolean {
  if (value === null || value === undefined) {
    return false;
  }

  const amount = normalizeMoney(String(value));

  return /^\d+(\.\d+)?$/.test(amount) && /[1-9]/.test(amount);
}
