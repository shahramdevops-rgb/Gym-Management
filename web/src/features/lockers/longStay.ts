/** A visit this long has probably been left open, or the key not given back (BUSINESS_RULES.md §6 *Long stay*). */
export const longStayHours = 3;

const longStayMs = longStayHours * 60 * 60 * 1000;

/** Said to a screen reader for a door or reserve place whose visit has gone on that long. */
export const longStayLabel = "بیش از ۳ ساعت";

export interface StayProgress {
  /** How far the bar is filled, from 0 at check-in to 1 at three hours; it stays full after that. */
  fraction: number;
  /** Three hours or more since check-in: the bar takes the warning colour. */
  isLong: boolean;
}

/**
 * How far a visit has gone towards a long stay, from its check-in moment and the current one.
 * A check-in that reads as later than `now` (the browser's clock a little behind the server's)
 * counts as just started rather than drawing a negative bar.
 */
export function stayProgress(checkedInAt: string, now: Date): StayProgress {
  const elapsed = now.getTime() - new Date(checkedInAt).getTime();
  return {
    fraction: Math.min(1, Math.max(0, elapsed / longStayMs)),
    isLong: elapsed >= longStayMs,
  };
}
