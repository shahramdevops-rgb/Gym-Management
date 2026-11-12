import { toPersianDigits } from "@/lib/format";

import type { DayOfWeek, SessionInput } from "./api";

/** The week as the gym counts it, from Saturday (BUSINESS_RULES.md §15 *The trainer's sessions*). */
export const weekDays: readonly DayOfWeek[] = [
  "Saturday",
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
];

export const dayNames: Record<DayOfWeek, string> = {
  Saturday: "شنبه",
  Sunday: "یکشنبه",
  Monday: "دوشنبه",
  Tuesday: "سه‌شنبه",
  Wednesday: "چهارشنبه",
  Thursday: "پنجشنبه",
  Friday: "جمعه",
};

/** JavaScript's `getUTCDay()` numbers the week as .NET's `DayOfWeek` does: 0 is Sunday. */
const byJsDay: readonly DayOfWeek[] = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
];

/** The day of the week an ISO business date falls on. */
export function dayOfWeek(iso: string): DayOfWeek {
  return byJsDay[new Date(`${iso}T00:00:00Z`).getUTCDay()]!;
}

// A business date is a calendar day: parsed as UTC midnight and formatted in UTC, as formatDate does.
const dayMonthFormatter = new Intl.DateTimeFormat("fa-IR", {
  calendar: "persian",
  timeZone: "UTC",
  day: "numeric",
  month: "long",
});

/** «۱۰ آبان»: a date of the timetable's twelve weeks, where the year says nothing. */
export function formatDayMonth(iso: string): string {
  return dayMonthFormatter.format(new Date(`${iso}T00:00:00Z`));
}

/** «شنبه ۱۰ آبان». */
export function formatDayLabel(iso: string): string {
  return `${dayNames[dayOfWeek(iso)]} ${formatDayMonth(iso)}`;
}

/** The API's `HH:mm:ss` as the editor's `HH:mm`. */
export const toMinutes = (time: string) => time.slice(0, 5);

/** «۱۳:۰۰ تا ۱۴:۳۰». */
export function formatSessionTimes(session: { start: string; end: string }): string {
  return `${toPersianDigits(toMinutes(session.start))} تا ${toPersianDigits(toMinutes(session.end))}`;
}

/** «۸ نفر». */
export function formatCapacity(capacity: number | string): string {
  return `${toPersianDigits(capacity)} نفر`;
}

/** «۳ از ۸ نفر»: the students in a session of a date, out of its capacity (15.6b). */
export function formatTaken(students: number | string, capacity: number | string): string {
  return `${toPersianDigits(students)} از ${formatCapacity(capacity)}`;
}

/** «شنبه ۱۳:۰۰ تا ۱۴:۰۰»: a weekly session of a student's fixed schedule. */
export function formatSlot(slot: { day: DayOfWeek; start: string; end: string }): string {
  return `${dayNames[slot.day]} ${formatSessionTimes(slot)}`;
}

/** The ISO business date `days` after `iso`. */
export function addDays(iso: string, days: number): string {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** The days from ISO date `from` to `to`; negative when `to` is earlier. */
export function daysBetween(from: string, to: string): number {
  return Math.round(
    (new Date(`${to}T00:00:00Z`).getTime() - new Date(`${from}T00:00:00Z`).getTime()) / 86_400_000,
  );
}

/**
 * A session's hours (BUSINESS_RULES.md §15 *The trainer's sessions*, the developer, 1405/08/13): it
 * starts at 07:00 or later and ends by 22:00, on the quarter hour.
 */
export const firstHour = 7;
export const lastHour = 22;
export const minuteStep = 15;
export const earliestStart = "07:00";
export const latestEnd = "22:00";

/**
 * What «افزودن سانس» puts in: right after the day's last session, as long and as full, or 08:00.
 * A normal session with no note: VIP and the standing note are the trainer's choice for each one.
 */
export function nextSession(sessions: readonly SessionInput[]): SessionInput {
  const last = sessions.at(-1);
  if (last === undefined) {
    return { start: "08:00", end: "09:00", capacity: 10, isVip: false, note: "" };
  }

  const toMinute = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));
  const length = toMinute(last.end) - toMinute(last.start);
  const start = toMinute(last.end);
  // Nothing past 22:00, the latest a session may end.
  const end = Math.min(start + Math.max(length, minuteStep), lastHour * 60);
  const asTime = (minute: number) =>
    `${String(Math.floor(minute / 60)).padStart(2, "0")}:${String(minute % 60).padStart(2, "0")}`;

  return start >= end
    ? { ...last, isVip: false, note: "" }
    : { start: asTime(start), end: asTime(end), capacity: last.capacity, isVip: false, note: "" };
}
