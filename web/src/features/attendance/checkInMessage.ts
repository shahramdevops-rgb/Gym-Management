import { formatMoney, toPersianDigits } from "@/lib/format";

import type { Attendance } from "./api";

/**
 * A successful check-in is not always unambiguous good news: no locker being free is still a
 * 201, not an error (BUSINESS_RULES.md §7), and so is a member who owes money — the gym runs open
 * accounts, so the visit is recorded and the amount is something to mention, not a refusal
 * (§0, §5 Member debt). This is the one place that turns the response into a sentence, shared by
 * the search results' one-click check-in and the member profile's.
 */
export function checkInResultMessage(attendance: Attendance): string {
  const locker =
    attendance.lockerNumber === null
      ? "ورود ثبت شد. در حال حاضر کمد آزادی نبود."
      : `ورود ثبت شد. کمد شماره ${toPersianDigits(attendance.lockerNumber)}`;

  const debt = Number(attendance.memberDebt);

  return debt > 0 ? `${locker} — بدهی این عضو ${formatMoney(debt)} است.` : locker;
}
