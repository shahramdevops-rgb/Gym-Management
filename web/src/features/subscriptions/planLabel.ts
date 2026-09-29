import { toPersianDigits } from "@/lib/format";

/** The three numbers that say what a subscription sold. The API sends them on every row that shows one. */
export interface PlanNumbers {
  durationDays: number | string;
  totalSessions: number | string;
  isSingleSession: boolean;
}

/**
 * How a plan reads (BUSINESS_RULES.md §3): «۱۲ جلسه - ۳۰ روزه», or «تک‌جلسه‌ای» for a single visit.
 *
 * Since task 6.5.6 a plan has no name: the desk builds each one from its days and sessions, so its
 * numbers are all there is to call it by. One function, so every screen that names a subscription —
 * the profile, the history, the visit box, the debt, the payments — says it the same way.
 */
export function planLabel(plan: PlanNumbers): string {
  if (plan.isSingleSession) {
    return "تک‌جلسه‌ای";
  }

  return `${toPersianDigits(plan.totalSessions)} جلسه - ${toPersianDigits(plan.durationDays)} روزه`;
}
