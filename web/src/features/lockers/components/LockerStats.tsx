import type { ReactNode } from "react";

import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { Locker } from "../api";
import { lockerState, lockerStateLabel, type LockerState } from "../lockerState";

const states = ["free", "occupied", "guest", "cardio", "outOfService"] as const;

/** A small door, drawn the way the map draws that state, for the legend. */
const swatchClass: Record<LockerState, string> = {
  free: "border-e-success",
  occupied: "border-e-destructive bg-door-held",
  guest: "border-e-guest bg-door-guest",
  cardio: "border-e-cardio bg-door-cardio",
  outOfService:
    "bg-[repeating-linear-gradient(135deg,var(--door-hatch)_0_2px,var(--door)_2px_4px)]",
};

/**
 * The strip above the map (BUSINESS_RULES.md §6 *The desk screen's look*): how many lockers are
 * free, occupied, held by a guest, held on a cardio-only visit and out of service, each beside a small door drawn like the map draws it, and what
 * the «بدهکار» tag and the long-stay bar mean.
 *
 * It is the map's legend too. `tools` sits at its other end, on the same row: the page puts the
 * name search there, and the usage view's switch beside it. While the map shows how often each
 * locker was used (§6 *Locker usage map*), `legend` takes the place of the counts.
 */
export function LockerStats({
  lockers,
  tools,
  legend,
}: {
  lockers: Locker[];
  tools?: ReactNode;
  /** Drawn instead of the counts and their legend; absent for the map as usual. */
  legend?: ReactNode;
}) {
  const counts: Record<LockerState, number> = {
    free: 0,
    occupied: 0,
    guest: 0,
    cardio: 0,
    outOfService: 0,
  };
  for (const locker of lockers) {
    counts[lockerState(locker)] += 1;
  }

  return (
    <section
      aria-label="وضعیت کمدها"
      className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 rounded-xl border bg-card px-4 py-3"
    >
      {legend ?? (
        <ul aria-label="راهنمای کمدها" className="flex flex-wrap items-center gap-x-6 gap-y-2">
          {states.map((state) => (
            <li key={state} className="flex items-center gap-2">
              <span
                aria-hidden
                className={cn(
                  "inline-block size-3.5 rounded-[3px] border border-e-[3px] border-door-border bg-door",
                  swatchClass[state],
                )}
              />
              <span className="text-2xl leading-none font-extrabold tabular-nums">
                {toPersianDigits(counts[state])}
              </span>{" "}
              <span className="text-xs text-muted-foreground">{lockerStateLabel[state]}</span>
            </li>
          ))}
          <li className="flex items-center gap-2 text-xs text-muted-foreground">
            <span
              aria-hidden
              className="rounded-es-sm bg-destructive px-1 text-[9px] leading-snug font-bold text-destructive-foreground"
            >
              بدهکار
            </span>
            بدهی دارد
          </li>
          <li className="flex items-center gap-2 text-xs text-muted-foreground">
            {/* Green on the right, red on the left: the bar fills from the right, as time passes. */}
            <span
              aria-hidden
              className="inline-block h-1 w-10 rounded-full bg-[linear-gradient(to_left,var(--success),color-mix(in_oklch,var(--success),var(--destructive)),var(--destructive))]"
            />
            زمان حضور تا ۳ ساعت
          </li>
        </ul>
      )}
      {tools}
    </section>
  );
}
