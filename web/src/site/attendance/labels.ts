import { jalaliMonthNames, jalaliPartsOf, jalaliToIso, toPersianDigits } from "@/lib/format";

import { toMinutes } from "../timetable/labels";
import type { SessionPresence } from "./api";

/** A student's state in a session as the trainer reads it (BUSINESS_RULES.md §15 *Attendance for the trainer*). */
export const presenceLabels: Record<SessionPresence, string> = {
  // More than 15 minutes before the start; present, as on time is (15.7m, the developer).
  Early: "زودتر از موعد",
  OnTime: "به‌موقع",
  Late: "با تأخیر",
  Absent: "غایب",
  NotYet: "هنوز نیامده",
  Leave: "مرخصی",
  Cancelled: "لغو شد",
  // Came to the gym after the session ended: they came, so not absent (15.7d, the developer).
  CameAfter: "بعد از سانس آمد",
};

export type PresenceVariant =
  "success" | "early" | "warning" | "destructive" | "outline" | "secondary" | "default";

/**
 * Green for on time, turquoise for early (its own colour, asked by the developer with 15.7m), amber
 * for late, blue for came after the session, red only for truly absent; a leave or a cancelled
 * session is no one's fault.
 */
export const presenceVariants: Record<SessionPresence, PresenceVariant> = {
  Early: "early",
  OnTime: "success",
  Late: "warning",
  Absent: "destructive",
  NotYet: "outline",
  Leave: "secondary",
  Cancelled: "outline",
  CameAfter: "default",
};

/** The API's `HH:mm:ss` as «۱۳:۱۰». */
export function formatClock(time: string): string {
  return toPersianDigits(toMinutes(time));
}

/** A number of minutes as «۲۵ دقیقه», «۱ ساعت» or «۲ ساعت و ۵ دقیقه». */
export function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) {
    return `${toPersianDigits(rest)} دقیقه`;
  }

  return rest === 0
    ? `${toPersianDigits(hours)} ساعت`
    : `${toPersianDigits(hours)} ساعت و ${toPersianDigits(rest)} دقیقه`;
}

/** «۳۰ دقیقه زودتر از موعد»: more than 15 minutes before the start, counted from it (15.7m). */
export const formatEarly = (minutes: number) => `${formatMinutes(minutes)} زودتر از موعد`;

/** «۲۵ دقیقه تأخیر»: late, counted from the start (15.7d). */
export const formatLate = (minutes: number) => `${formatMinutes(minutes)} تأخیر`;

/**
 * A visit's check-out as the trainer reads it (15.7d): «خروج ۱۴:۴۰»; «داخل است» while the visit is
 * open; «خروج ثبت نشد» when the nightly auto-checkout closed it (§7), never its midnight.
 */
export function formatCheckOut(
  checkedOutAt: string | null | undefined,
  autoCheckedOut: boolean,
): string {
  if (autoCheckedOut) {
    return "خروج ثبت نشد";
  }

  return checkedOutAt === null || checkedOutAt === undefined
    ? "داخل است"
    : `خروج ${formatClock(checkedOutAt)}`;
}

/** A visit outside the sessions: «ورود ۱۸:۰۰ · خروج ۱۹:۳۰». */
export function formatVisit(visit: {
  checkedInAt: string;
  checkedOutAt?: string | null;
  autoCheckedOut: boolean;
}): string {
  return `ورود ${formatClock(visit.checkedInAt)} · ${formatCheckOut(visit.checkedOutAt, visit.autoCheckedOut)}`;
}

/** The ISO day `by` days later (or earlier, when negative). */
export function shiftDay(iso: string, by: number): string {
  const [year, month, day] = iso.split("-").map(Number);

  return new Date(Date.UTC(year!, month! - 1, day! + by)).toISOString().slice(0, 10);
}

/** A Jalali month: the year and the month, 1 to 12. */
export interface JalaliMonth {
  year: number;
  month: number;
}

/** The Jalali month an ISO business date falls in. */
export function monthOf(iso: string): JalaliMonth {
  const parts = jalaliPartsOf(iso);
  if (parts === null) {
    throw new Error(`Not an ISO date: ${iso}`);
  }

  return { year: parts.year, month: parts.month };
}

/** The month `by` months later (or earlier, when negative). */
export function shiftMonth({ year, month }: JalaliMonth, by: number): JalaliMonth {
  const index = year * 12 + (month - 1) + by;

  return { year: Math.floor(index / 12), month: (index % 12) + 1 };
}

/** The month's first and last ISO dates: 31, 30 or 29 days, whichever is real. */
export function monthRange({ year, month }: JalaliMonth): { from: string; to: string } {
  const from = jalaliToIso(year, month, 1)!;
  const to = [31, 30, 29].map((day) => jalaliToIso(year, month, day)).find((iso) => iso !== null)!;

  return { from, to };
}

/** «آبان ۱۴۰۵». */
export function formatMonth({ year, month }: JalaliMonth): string {
  return `${jalaliMonthNames[month - 1]} ${toPersianDigits(year)}`;
}

export const sameMonth = (a: JalaliMonth, b: JalaliMonth) =>
  a.year === b.year && a.month === b.month;
