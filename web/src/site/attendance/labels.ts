import { jalaliMonthNames, jalaliPartsOf, jalaliToIso, toPersianDigits } from "@/lib/format";

import { toMinutes } from "../timetable/labels";
import type { SessionPresence } from "./api";

/** A student's state in a session as the trainer reads it (BUSINESS_RULES.md §15 *Attendance for the trainer*). */
export const presenceLabels: Record<SessionPresence, string> = {
  OnTime: "به‌موقع",
  Late: "با تأخیر",
  Absent: "غایب",
  NotYet: "هنوز نیامده",
  Leave: "مرخصی",
  Cancelled: "لغو شد",
};

export type PresenceVariant = "success" | "warning" | "destructive" | "outline" | "secondary";

/** Green for came, amber for late, red for absent; a leave or a cancelled session is no one's fault. */
export const presenceVariants: Record<SessionPresence, PresenceVariant> = {
  OnTime: "success",
  Late: "warning",
  Absent: "destructive",
  NotYet: "outline",
  Leave: "secondary",
  Cancelled: "outline",
};

/** «بعد از سانس آمد»: absent, but checked in after the session ended. */
export const cameAfterLabel = "بعد از سانس آمد";

/** The API's `HH:mm:ss` as «۱۳:۱۰». */
export function formatClock(time: string): string {
  return toPersianDigits(toMinutes(time));
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
