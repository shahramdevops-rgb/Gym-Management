/** The periods the usage view offers, in days, today included (BUSINESS_RULES.md §6 *Locker usage map*). */
export const usagePeriods = [7, 30, 90] as const;

export type UsagePeriod = (typeof usagePeriods)[number];

/** The period picked when the usage view is turned on. */
export const defaultUsagePeriod: UsagePeriod = 30;

/** Written on a door, and in the legend, for a locker nobody used in the period. */
export const unusedLabel = "استفاده نشده";

/** How many shades the doors are coloured in, from least to most used. */
export const usageLevels = 5;

/**
 * A door's shade in the usage view: `0` for a locker nobody used in the period (it has a colour of
 * its own), otherwise 1 to {@link usageLevels}, relative to the most used locker in the period. Any
 * use at all is at least 1, so a locker used once never looks like one never used; the most used is
 * always the darkest.
 */
export function usageLevel(uses: number, mostUses: number): number {
  if (uses <= 0 || mostUses <= 0) {
    return 0;
  }
  return Math.max(1, Math.ceil((uses / mostUses) * usageLevels));
}
