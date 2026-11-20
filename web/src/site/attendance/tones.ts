import type { PresenceVariant } from "./labels";

/** A count box's colour: its state's badge colour, or neutral. */
export type BoxTone =
  "neutral" | "early" | "success" | "warning" | "outsideSession" | "destructive" | "otherDay";

interface ToneClasses {
  /** The box at rest: a pale tint of the colour. */
  box: string;
  /** Under the pointer, for a box that opens or filters. */
  hover: string;
  /** The number, in the colour (a darker ink where the colour itself is light). */
  count: string;
  /** Pressed on the day page: filled with the colour, as its badge is. */
  filled: string;
}

/**
 * Every count box has its state's colour (BUSINESS_RULES.md §15 *The student's profile and every
 * visit in the month*, asked by the developer with 15.7p): turquoise early, green on time and
 * present, amber late, dark blue «خارج از سانس», red absent, purple «روز دیگر»; neutral where the
 * badge is («شاگردان», «مرخصی», «لغو شد»). The tint is mixed with the card's colour, not
 * see-through: the month's and the day page's boxes sit on the page's coloured background, which
 * made a see-through tint muddy. Whole class names, so Tailwind finds them.
 */
export const boxTones: Record<BoxTone, ToneClasses> = {
  neutral: {
    box: "bg-muted",
    hover: "hover:bg-accent",
    count: "",
    filled: "bg-foreground text-background",
  },
  early: {
    box: "bg-[color-mix(in_oklab,var(--early)_16%,var(--card))]",
    hover: "hover:bg-[color-mix(in_oklab,var(--early)_26%,var(--card))]",
    count: "text-early-ink",
    filled: "bg-early text-white dark:text-neutral-950",
  },
  success: {
    box: "bg-[color-mix(in_oklab,var(--success)_16%,var(--card))]",
    hover: "hover:bg-[color-mix(in_oklab,var(--success)_26%,var(--card))]",
    count: "text-success-ink",
    filled: "bg-success text-success-foreground",
  },
  warning: {
    box: "bg-[color-mix(in_oklab,var(--warning)_20%,var(--card))]",
    hover: "hover:bg-[color-mix(in_oklab,var(--warning)_30%,var(--card))]",
    count: "text-warning-ink",
    filled: "bg-warning text-warning-foreground",
  },
  outsideSession: {
    box: "bg-[color-mix(in_oklab,var(--outside-session)_14%,var(--card))]",
    hover: "hover:bg-[color-mix(in_oklab,var(--outside-session)_24%,var(--card))]",
    count: "text-outside-session",
    filled: "bg-outside-session text-white dark:text-neutral-950",
  },
  destructive: {
    box: "bg-[color-mix(in_oklab,var(--destructive)_13%,var(--card))]",
    hover: "hover:bg-[color-mix(in_oklab,var(--destructive)_22%,var(--card))]",
    count: "text-destructive",
    filled: "bg-destructive text-white",
  },
  otherDay: {
    box: "bg-[color-mix(in_oklab,var(--other-day)_16%,var(--card))]",
    hover: "hover:bg-[color-mix(in_oklab,var(--other-day)_26%,var(--card))]",
    count: "text-other-day",
    filled: "bg-other-day text-white dark:text-neutral-950",
  },
};

/** The box tone of a badge's colour, so a box and its rows' badges never differ. */
export function toneOf(variant: PresenceVariant): BoxTone {
  switch (variant) {
    case "early":
    case "success":
    case "warning":
    case "outsideSession":
    case "destructive":
    case "otherDay":
      return variant;
    case "outline":
    case "secondary":
      return "neutral";
  }
}
