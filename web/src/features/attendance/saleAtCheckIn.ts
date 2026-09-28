/**
 * Codes that mean "this member cannot come in today because of their subscription", as opposed to
 * anything else check-in can refuse (an inactive member, someone already inside, a race).
 *
 * This list is what turns a refusal into the offer of a sale. It is deliberately a list of
 * reasons rather than a guess made before the call: the API decides whether someone can come in,
 * and asking it is the only way to be sure. Deriving the same answer in the browser would mean a
 * second copy of the rule in BUSINESS_RULES.md §4 and §7, and the copy would be the one that was
 * wrong (the same reasoning that keeps locker occupancy derived, task 6.5.1).
 */
const needsASubscription = new Set([
  "Attendance.NoSubscription",
  "Subscriptions.Expired",
  "Subscriptions.NoSessionsLeft",
  "Subscriptions.Frozen",
  "Subscriptions.NotStarted",
  "Subscriptions.NextStartsTomorrow",
  "Subscriptions.Cancelled",
]);

/**
 * The refusals after which a new plan starts today, so selling one in the check-in box lets the
 * member straight in (roadmap 6.5.7). After the others the member already holds something that
 * a new plan would queue behind — a frozen plan, one bought for later — so it could not start
 * today, and the box offers the single visit and the profile instead.
 *
 * The API still has the last word: a plan sold with a check-in that cannot happen today is
 * refused and rolled back with it, so a wrong entry here costs a message, never a stray sale.
 */
const planStartsToday = new Set([
  "Attendance.NoSubscription",
  "Subscriptions.Expired",
  "Subscriptions.NoSessionsLeft",
  "Subscriptions.Cancelled",
]);

function codeOf(problem: unknown): string | undefined {
  const code =
    typeof problem === "object" && problem !== null && "code" in problem
      ? (problem as { code?: unknown }).code
      : undefined;

  return typeof code === "string" ? code : undefined;
}

/** Whether a failed check-in failed for want of a usable subscription. */
export function isMissingSubscription(problem: unknown): boolean {
  const code = codeOf(problem);

  return code !== undefined && needsASubscription.has(code);
}

/** Whether, after this refusal, a plan sold now would start today. */
export function canSellPlanForToday(problem: unknown): boolean {
  const code = codeOf(problem);

  return code !== undefined && planStartsToday.has(code);
}
