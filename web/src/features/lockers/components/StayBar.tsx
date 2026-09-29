import type { CSSProperties } from "react";

import { cn } from "@/lib/utils";

import type { StayProgress } from "../longStay";

/**
 * The thin bar along the bottom of an occupied door or a used reserve place (BUSINESS_RULES.md §6
 * *Long stay*). It fills from the right, the way a Persian reader expects progress to go, and its
 * colour moves with it: green at check-in, through yellow, to red at three hours. From then on the
 * full red bar blinks, glowing and then nearly fading out, and stays still for anyone who asked
 * for less motion.
 * It takes no corner and no line of text; the button it sits in says it to a screen reader.
 *
 * The colour is one `color-mix` of the success and destructive tokens, weighted by `--stay-mix`.
 * Mixing in oklch walks the hue from green to red through yellow, and the tokens keep it right in
 * both the light and the dark palette.
 *
 * `dir="rtl"` is set on it because the map is `dir="ltr"` (the cabinets stand left to right), and
 * the fill is placed with `start-0`.
 */
export function StayBar({ progress }: { progress: StayProgress }) {
  const percent = `${Math.round(progress.fraction * 100)}%`;

  return (
    <span
      aria-hidden
      dir="rtl"
      data-testid="stay-bar"
      data-long={progress.isLong || undefined}
      className="pointer-events-none absolute inset-x-0 bottom-0 h-1 bg-foreground/10"
    >
      <span
        data-testid="stay-bar-fill"
        className={cn(
          "absolute start-0 inset-y-0 rounded-e-full",
          "bg-[color-mix(in_oklch,var(--destructive)_var(--stay-mix),var(--success))]",
          progress.isLong && "animate-stay-blink motion-reduce:animate-none",
        )}
        style={{ width: percent, "--stay-mix": percent } as CSSProperties}
      />
    </span>
  );
}
