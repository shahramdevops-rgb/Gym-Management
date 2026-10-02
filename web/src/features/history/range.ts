/**
 * How many days before today Staff may read payments (BUSINESS_RULES.md §12 History). The API
 * enforces the same number (`PaymentHistoryWindow.StaffDaysBeforeToday`); this copy only lets the
 * page say so before asking.
 */
export const staffPaymentDaysBeforeToday = 3;

/**
 * An ISO date some days earlier (`2026-10-02`, 3 → `2026-09-29`). Worked out on a UTC calendar
 * date, so no clock change can move it by a day.
 */
export function isoDaysBefore(iso: string, days: number): string {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() - days);

  return date.toISOString().slice(0, 10);
}

/** The first day Staff may ask for payments, given the gym's today. */
export function staffEarliestPaymentDay(today: string): string {
  return isoDaysBefore(today, staffPaymentDaysBeforeToday);
}
