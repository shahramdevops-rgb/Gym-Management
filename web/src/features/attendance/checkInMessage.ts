import { toPersianDigits } from "@/lib/format";

import type { Attendance } from "./api";

/**
 * A successful check-in is not always unambiguous good news: no locker being free is still a
 * 201, not an error (BUSINESS_RULES.md §7), so the front desk still needs to see it. This is the
 * one place that turns the response into a sentence, shared by the search results' one-click
 * check-in and the member profile's.
 */
export function checkInResultMessage(attendance: Attendance): string {
  return attendance.lockerNumber === null
    ? "ورود ثبت شد. در حال حاضر کمد آزادی نبود."
    : `ورود ثبت شد. کمد شماره ${toPersianDigits(attendance.lockerNumber)}`;
}
