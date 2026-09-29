import type { LockerState } from "../lockerState";

/**
 * The look every locker door shares, and every reserve place with it, so the two can never drift
 * apart (BUSINESS_RULES.md §6 *The desk screen's look*): a dark tile in a thin frame, with a thicker
 * edge down its right side (`border-e`, the right inside the map's `dir="ltr"`) that the state
 * colours.
 */
export const doorFace =
  "relative overflow-hidden rounded-lg border border-e-[3px] border-door-border bg-door shadow-sm shadow-black/20";

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
 * Each state's edge, face and text colour: green when free; tinted red from the top corner with a
 * red edge when held; hatched when out of service.
 */
export const doorStateClass: Record<LockerState, string> = {
  free: "border-e-success text-success",
  occupied:
    "border-e-destructive text-foreground bg-[linear-gradient(135deg,var(--door-held),var(--door)_70%)]",
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
  "border-dashed border-door-unused text-door-unused",
  "border-e-door-use text-foreground bg-[color-mix(in_oklch,var(--door-use)_14%,var(--door))]",
  "border-e-door-use text-foreground bg-[color-mix(in_oklch,var(--door-use)_30%,var(--door))]",
  "border-e-door-use text-foreground bg-[color-mix(in_oklch,var(--door-use)_46%,var(--door))]",
  "border-e-door-use text-white bg-[color-mix(in_oklch,var(--door-use)_64%,var(--door))]",
  "border-e-door-use text-white bg-[color-mix(in_oklch,var(--door-use)_84%,var(--door))]",
];
