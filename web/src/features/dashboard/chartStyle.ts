import type { TooltipContentProps } from "recharts";

/** The axis and grid ink of every dashboard chart: recessive, one step off the card. */
export const axisTick = { fill: "var(--muted-foreground)", fontSize: 12 };
export const gridStroke = "var(--border)";

/** The data row a Recharts tooltip is over, typed as the chart's own rows. */
export function hoveredRow<T>(props: TooltipContentProps): T | undefined {
  return props.payload[0]?.payload as T | undefined;
}

/** RIGHT-TO-LEFT MARK: invisible, and makes the text after it start as right-to-left text. */
const rightToLeftMark = String.fromCodePoint(0x200f);

/**
 * A label for inside a Recharts chart, which is drawn left to right (see `RevenueChart`): without
 * the mark, «۶ میلیون» or «مهر ۱۴۰۵» in a left-to-right line puts the number on the wrong side of
 * the word. With it the label reads as it would anywhere else on the page.
 */
export function svgText(text: string): string {
  return `${rightToLeftMark}${text}`;
}
