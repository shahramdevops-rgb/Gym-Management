import type { CSSProperties } from "react";

import { cn } from "@/lib/utils";

/** Said to a screen reader, and on hover, for a door or reserve place whose member's birthday is today. */
export const birthdayLabel = "امروز تولدش است";

/**
 * The celebration on a birthday member's door (BUSINESS_RULES.md §6 *The desk panel*): a ring of
 * party colours turning around its edge, and confetti falling inside it, behind the number and the
 * name so both stay readable. Both are only for the eye; the door's label says «امروز تولدش است».
 *
 * For anyone who has asked for less motion the ring stands still and no confetti falls.
 */
export function Celebration() {
  return (
    <>
      <PartyRing />
      <Confetti />
    </>
  );
}

/**
 * A 2px ring of party colours inside the edge of the door or place, turning slowly. The gradient
 * fills the whole box and a mask cuts out all but its padding, so it follows any rounding; `--party-angle` is a registered property, so it can be animated.
 */
function PartyRing() {
  return (
    <span
      aria-hidden
      data-testid="party-ring"
      className={cn(
        "pointer-events-none absolute inset-0 rounded-[inherit] p-[2px]",
        "bg-[conic-gradient(from_var(--party-angle),var(--party-pink),var(--party-gold),var(--party-mint),var(--party-sky),var(--party-violet),var(--party-pink))]",
        "[mask:linear-gradient(#000_0_0)_content-box_exclude,linear-gradient(#000_0_0)]",
        "animate-party-spin motion-reduce:animate-none",
      )}
    />
  );
}

/**
 * Where each piece starts across the door (percent), when it starts and how long it takes to fall
 * (seconds), which way it drifts, and its colour and shape. Fixed, not random, so every birthday
 * door falls the same way and a test sees the same thing twice.
 */
const pieces = [
  { left: 8, delay: 0, duration: 2.6, drift: "0.5rem", className: "h-2 w-1 bg-party-pink" },
  { left: 22, delay: 1.1, duration: 3.1, drift: "-0.4rem", className: "size-1.5 bg-party-gold" },
  { left: 36, delay: 0.5, duration: 2.4, drift: "0.3rem", className: "h-1 w-2 bg-party-mint" },
  { left: 50, delay: 1.7, duration: 2.9, drift: "-0.6rem", className: "h-2 w-1 bg-party-sky" },
  { left: 63, delay: 0.2, duration: 3.3, drift: "0.4rem", className: "size-1.5 bg-party-violet" },
  { left: 76, delay: 1.3, duration: 2.5, drift: "-0.3rem", className: "h-2 w-1 bg-party-gold" },
  { left: 88, delay: 0.8, duration: 3, drift: "-0.5rem", className: "h-1 w-2 bg-party-pink" },
  { left: 44, delay: 2.2, duration: 2.7, drift: "0.6rem", className: "size-1 bg-party-mint" },
] as const;

function Confetti() {
  return (
    <span
      aria-hidden
      className="pointer-events-none absolute inset-0 overflow-hidden rounded-[inherit] motion-reduce:hidden"
    >
      {pieces.map((piece) => (
        <span
          key={`${piece.left}-${piece.delay}`}
          className={cn(
            "absolute top-0 animate-confetti-fall rounded-[1px] opacity-0",
            piece.className,
          )}
          style={
            {
              left: `${piece.left}%`,
              animationDelay: `${piece.delay}s`,
              animationDuration: `${piece.duration}s`,
              "--confetti-drift": piece.drift,
            } as CSSProperties
          }
        />
      ))}
    </span>
  );
}
