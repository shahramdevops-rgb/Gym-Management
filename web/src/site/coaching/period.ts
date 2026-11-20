import { formatDate, toPersianDigits } from "@/lib/format";

import { addDays, daysBetween } from "../timetable/labels";

/*
 * The coaching period (BUSINESS_RULES.md §15 *The coaching period*, roadmap 15.7h), as the API holds
 * it (`CoachingPeriod`): the trainer types its days when accepting a request and at each «تمدید».
 */

/** «One month», the number the box starts at. */
export const periodDefaultDays = 30;
export const periodMinDays = 7;
/** The timetable's 12 weeks: no session can be placed further on. */
export const periodMaxDays = 84;
/** The fewest days left of a link made before 15.7h. */
export const periodMinDaysLeft = 1;

/**
 * From how many days left the trainer's list marks a student: the API's `periodSoonDays`, the SMS
 * settings' number for the coaching period's last days (BUSINESS_RULES.md §15 *The coaching period*,
 * roadmap 15.7n, in place of the 3 days of 15.7h), so the trainer sees it the day the student gets
 * the SMS, on or off.
 */
export function soonDaysOf(students: { periodSoonDays: number | string }): number {
  return Number(students.periodSoonDays);
}

export type PeriodState =
  | { kind: "unset" }
  | { kind: "over"; endsOn: string }
  | { kind: "running"; endsOn: string; daysLeft: number };

/** Where a period stands on `today`: its last day still counts, so on it one day is left. */
export function periodState(endsOn: string | null | undefined, today: string): PeriodState {
  if (endsOn === null || endsOn === undefined) {
    return { kind: "unset" };
  }
  const daysLeft = daysBetween(today, endsOn) + 1;
  return daysLeft <= 0 ? { kind: "over", endsOn } : { kind: "running", endsOn, daysLeft };
}

/**
 * Whether a student is among the active ones of «شاگردان من» (BUSINESS_RULES.md §15 *«شاگردان من» in
 * two parts*, roadmap 15.7l): the coaching runs, or the student is from before the periods and
 * waits for the days left. One whose period is over and was not renewed is inactive: still the
 * trainer's student, in the closed part below; a «تمدید» brings them back.
 */
export function isActiveStudent(endsOn: string | null | undefined, today: string): boolean {
  return periodState(endsOn, today).kind !== "over";
}

/** Which students a list shows (BUSINESS_RULES.md §15 *«شاگردان من» in two parts*, 15.7p). */
export type StudentPart = "active" | "inactive";

/**
 * The students of one part on `today`. Here rather than beside the list pages, so the home's cards
 * (`TrainerArea`) do not pull the lazy pages into the first download (15.7c).
 */
export function studentsOf<T extends { periodEndsOn?: string | null }>(
  students: readonly T[],
  part: StudentPart,
  today: string,
): T[] {
  return students.filter(
    (student) => isActiveStudent(student.periodEndsOn, today) === (part === "active"),
  );
}

/** «دوره تا ۱۹ آذر ۱۴۰۵، ۱۲ روز مانده», or that it is over, or not given yet. */
export function periodLine(state: PeriodState): string {
  switch (state.kind) {
    case "unset":
      return "دورهٔ مربی‌گری مشخص نشده است";
    case "over":
      return `دورهٔ مربی‌گری ${formatDate(state.endsOn)} تمام شد`;
    case "running":
      return `دورهٔ مربی‌گری تا ${formatDate(state.endsOn)}، ${daysLeftText(state.daysLeft)}`;
  }
}

/**
 * The period on a student's profile (15.7p), beside its name «دورهٔ مربی‌گری»: «تا ۱۹ آذر ۱۴۰۵،
 * ۱۲ روز مانده», «۱۹ آذر ۱۴۰۵ تمام شد» or «مشخص نشده است».
 */
export function periodValue(state: PeriodState): string {
  switch (state.kind) {
    case "unset":
      return "مشخص نشده است";
    case "over":
      return `${formatDate(state.endsOn)} تمام شد`;
    case "running":
      return `تا ${formatDate(state.endsOn)}، ${daysLeftText(state.daysLeft)}`;
  }
}

export function daysLeftText(daysLeft: number): string {
  return daysLeft === 1 ? "امروز روز آخر است" : `${toPersianDigits(daysLeft)} روز مانده`;
}

/**
 * The short mark in «شاگردان من»: only when the trainer has something to do. Days left read «۲ روز»,
 * without «مانده», and the last one «روز آخر» (the developer, 1405/08/20: the mark stays small),
 * from `soonDays` days left (`soonDaysOf`).
 */
export function periodMark(state: PeriodState, soonDays: number): string | null {
  switch (state.kind) {
    case "unset":
      return "دوره را مشخص کنید";
    case "over":
      return "دوره تمام شد";
    case "running":
      if (state.daysLeft > soonDays) {
        return null;
      }
      return state.daysLeft === 1 ? "روز آخر" : `${toPersianDigits(state.daysLeft)} روز`;
  }
}

/**
 * The first day of the next period, as the API works it out (`TrainerLink.Renew`): the day after the
 * last one, or today when there is none or it is over.
 */
export function nextPeriodStart(endsOn: string | null | undefined, today: string): string {
  return endsOn !== null && endsOn !== undefined && endsOn >= today ? addDays(endsOn, 1) : today;
}

/** The last day of a period of `days` days from `startsOn`. */
export function periodEnd(startsOn: string, days: number): string {
  return addDays(startsOn, days - 1);
}

/**
 * The question before «تمدید» (the developer's wording, 1405/08/24), on «برنامهٔ ثابت» and on a
 * student's «درخواست تمدید» (15.7o): «دورهٔ مربی‌گری مینا رضایی تا ۱۹ آذر ۱۴۰۵ تمدید شود؟».
 */
export function renewQuestion(fullName: string, lastDay: string): string {
  return `دورهٔ مربی‌گری ${fullName} تا ${formatDate(lastDay)} تمدید شود؟`;
}
