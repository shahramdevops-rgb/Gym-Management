import type { LockerState } from "../lockerState";

/**
 * The look every locker door shares, and every reserve place with it, so the two can never drift
 * apart (BUSINESS_RULES.md §6 *The desk screen's look*): a tile in the page's own colour with no
 * frame (13.2h, asked by the developer), only a thick edge down its right side (`border-e`, the
 * right inside the map's `dir="ltr"`) that the state colours.
 */
export const doorFace =
  "relative overflow-hidden rounded-lg border-e-[3px] border-door-border bg-door";

/**
 * The door rises a little under the mouse and sinks back when pressed; a disabled one does
 * neither. Nothing moves for anyone who has asked for less motion.
 */
export const doorMotion = [
  "transition-[translate,scale,box-shadow,opacity] duration-200 ease-out",
  "enabled:hover:-translate-y-1 enabled:hover:shadow-lg enabled:hover:shadow-black/40",
  "enabled:active:translate-y-0 enabled:active:scale-[0.97]",
  "motion-reduce:transition-none motion-reduce:enabled:hover:translate-y-0",
  "focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
  "disabled:cursor-not-allowed disabled:opacity-40",
].join(" ");

/**
 * Each state's edge, face and text colour: tinted green from the top corner with a green edge when
 * free, the number a soft grey, lighter than an out-of-service door's (both changed by the
 * developer, 1405/07/20: the number was green, the face plain); tinted red from the top corner with a red edge when held by a member, the same in blue when held
 * by a guest and in yellow on a cardio-only visit; hatched when out of service.
 */
export const doorStateClass: Record<LockerState, string> = {
  free: "border-e-success text-foreground/75 bg-[linear-gradient(135deg,var(--door-free),var(--door)_70%)]",
  occupied:
    "border-e-destructive text-foreground bg-[linear-gradient(135deg,var(--door-held),var(--door)_70%)]",
  guest:
    "border-e-guest text-foreground bg-[linear-gradient(135deg,var(--door-guest),var(--door)_70%)]",
  cardio:
    "border-e-cardio text-foreground bg-[linear-gradient(135deg,var(--door-cardio),var(--door)_70%)]",
  outOfService:
    "text-muted-foreground bg-[repeating-linear-gradient(135deg,var(--door-hatch)_0_6px,var(--door)_6px_12px)]",
};

/**
 * A door's face in the usage view (BUSINESS_RULES.md §6 *Locker usage map*), by its shade from
 * `usageLevel`: a dashed amber frame and number for a locker nobody used in the period, otherwise
 * the usage colour mixed into the door, stronger the more it was used, with the number turning
 * white once the colour is deep enough to need it.
 */
export const usageDoorClass: readonly string[] = [
  "border border-dashed border-door-unused text-door-unused",
  "border-e-door-use text-foreground bg-[color-mix(in_oklch,var(--door-use)_14%,var(--door))]",
  "border-e-door-use text-foreground bg-[color-mix(in_oklch,var(--door-use)_30%,var(--door))]",
  "border-e-door-use text-foreground bg-[color-mix(in_oklch,var(--door-use)_46%,var(--door))]",
  "border-e-door-use text-white bg-[color-mix(in_oklch,var(--door-use)_64%,var(--door))]",
  "border-e-door-use text-white bg-[color-mix(in_oklch,var(--door-use)_84%,var(--door))]",
];
