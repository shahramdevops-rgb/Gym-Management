import { Lock } from "lucide-react";
import { useState, type CSSProperties, type ReactNode } from "react";

import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { Locker } from "../api";
import { cabinetColumns, columnsPerCabinet, lockerZones } from "../layout";
import {
  isHeld,
  lockerHolderName,
  lockerState,
  lockerStateLabel,
  type LockerState,
} from "../lockerState";
import { longStayLabel, type StayProgress } from "../longStay";
import { unusedLabel, usageLevel } from "../usage";
import { birthdayLabel, Celebration } from "./Celebration";
import { doorFace, doorMotion, doorStateClass, usageDoorClass } from "./doorStyle";
import { StayBar } from "./StayBar";

/** Doors side by side in the widest zone (inside the changing room: seven cabinets of two). */
const widestRowDoors =
  Math.max(...lockerZones.map((zone) => zone.groups.flatMap((group) => group.cabinets).length)) *
  columnsPerCabinet;

/**
 * A door's width: as wide as the map lets the widest zone be, never smaller than a fingertip and
 * never larger than a real door would read. `100cqw` is the map's own width (it is the query
 * container); 12rem leaves room for the gaps between doors, cabinets and groups.
 * Every door gets the same size whatever is written on it, so a long name can never grow one.
 */
const doorWidth = `clamp(2.75rem, calc((100cqw - 12rem) / ${widestRowDoors}), 6rem)`;

/** Fixed square doors, sized by `--door-size` on the map. */
const doorSize = "w-(--door-size) shrink-0 aspect-square";

/** Written on the tag of a door whose holder owes money. */
const debtorLabel = "بدهکار";

/** The one-time pulse on a door that changed, in its new colour. */
const changedRingClass: Record<LockerState, string> = {
  free: "ring-success",
  occupied: "ring-destructive",
  guest: "ring-guest",
  outOfService: "ring-muted-foreground",
};

interface LockerMapProps {
  lockers: Locker[];
  onSelect: (locker: Locker) => void;
  /**
   * `desk` (the default): every locker opens its box. `pick`: only free lockers can be clicked,
   * for choosing where to move a visit (BUSINESS_RULES.md §7 *Moving to another locker*).
   */
  mode?: "desk" | "pick";
  /**
   * Shown at the right-hand end of the first zone, in the room its five cabinets leave beside the
   * seven below (BUSINESS_RULES.md §6 *The desk panel*). It wraps under the zone on a narrow screen.
   */
  aside?: ReactNode;
  /** A locker to make blink, so the desk finds it at a glance; `null` or absent for none. */
  highlightedLockerId?: string | null;
  /**
   * How long each held locker's visit has gone on, by locker id, for the bar along the bottom of
   * its door (BUSINESS_RULES.md §6 *Long stay*). Absent where the time does not matter, such as
   * choosing a locker to move to.
   */
  stays?: ReadonlyMap<string, StayProgress>;
  /**
   * The lockers whose holder matches the name being searched for (BUSINESS_RULES.md §6 *Finding a
   * member on the map*): they are lifted and outlined, every other door fades. `null` or absent
   * while nothing is being searched for.
   */
  foundLockerIds?: ReadonlySet<string> | null;
  /**
   * The lockers whose holder's birthday is today (BUSINESS_RULES.md §6 *The desk panel*): their
   * doors celebrate. Absent where it does not matter, such as choosing a locker to move to.
   */
  birthdayLockerIds?: ReadonlySet<string>;
  /**
   * The usage view (BUSINESS_RULES.md §6 *Locker usage map*): how many visits each locker had in
   * the period, by locker id. When given, every door is shaded by it and shows the count instead of
   * who holds it, and no door can be clicked. Absent for the map as usual.
   */
  usage?: ReadonlyMap<string, number>;
}

/**
 * The gym's lockers drawn the way they stand (BUSINESS_RULES.md §6 *Where they stand*): each a
 * door-shaped button, with a green number and edge when free, tinted red with a red edge when
 * someone holds it (their name on it and on hover), hatched with a lock when out of service. The
 * desk clicks the locker it gives a member, the way a cinema seat is booked.
 *
 * The cabinets are laid out left to right, as on the wall, even though the page is right-to-left:
 * `dir="ltr"` on each zone keeps locker 1 at the left end where it really is. No location words
 * are written on the map; the drawing already says where a locker is, and the zone names are
 * only for a screen reader. A cabinet has no frame: the gap between cabinets is twice the gap
 * between doors, which is enough to tell them apart.
 *
 * The doors grow with the screen so the whole width is used, rise a little under the mouse, and
 * pulse once when a refresh changes them (§6 *The desk screen's look*). A holder who owes money
 * gets a red «بدهکار» tag on the door's top-right corner, and a thin bar along its bottom fills
 * over three hours of the visit. A holder whose birthday is today gets a party on their door: a
 * turning ring of colours and falling confetti (§6 *The desk panel*).
 */
export function LockerMap({
  lockers,
  onSelect,
  mode = "desk",
  aside,
  highlightedLockerId = null,
  stays,
  foundLockerIds = null,
  birthdayLockerIds,
  usage,
}: LockerMapProps) {
  const byNumber = new Map(lockers.map((locker) => [Number(locker.number), locker]));
  const changed = useChangedDoors(lockers);
  const mostUses = usage === undefined ? 0 : Math.max(0, ...usage.values());

  return (
    <div className="@container space-y-6" style={{ "--door-size": doorWidth } as CSSProperties}>
      {lockerZones.map((zone, zoneIndex) => (
        <section key={zone.name} aria-label={zone.name}>
          {/* The padding keeps a lifted door, and the ring around a found one, inside the zone's
              scrolling edge, which would otherwise cut them off. */}
          <div
            dir="ltr"
            className="flex flex-wrap items-start gap-10 overflow-x-auto px-1 pt-3 pb-2"
          >
            {zone.groups.map((group) => (
              <div key={group.cabinets[0]} className="flex gap-4">
                {group.cabinets.map((first) => (
                  <div key={first} className="flex gap-2" data-testid={`cabinet-${first}`}>
                    {cabinetColumns(first).map((column) => (
                      <div key={column[0]} className="flex flex-col gap-2">
                        {column.map((number) => {
                          const locker = byNumber.get(number);
                          if (usage !== undefined && locker !== undefined) {
                            return (
                              <UsageDoor
                                key={number}
                                number={number}
                                outOfService={locker.isOutOfService}
                                uses={usage.get(locker.id) ?? 0}
                                mostUses={mostUses}
                              />
                            );
                          }
                          return (
                            <LockerDoor
                              key={number}
                              number={number}
                              locker={locker}
                              mode={mode}
                              highlighted={
                                highlightedLockerId !== null && locker?.id === highlightedLockerId
                              }
                              found={
                                foundLockerIds === null
                                  ? null
                                  : locker !== undefined && foundLockerIds.has(locker.id)
                              }
                              changedRound={
                                locker !== undefined && changed.ids.has(locker.id)
                                  ? changed.round
                                  : null
                              }
                              birthday={
                                locker !== undefined && (birthdayLockerIds?.has(locker.id) ?? false)
                              }
                              stays={stays}
                              onSelect={onSelect}
                            />
                          );
                        })}
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            ))}
            {/* ms-auto here, inside dir="ltr", is a left margin: it pushes the aside to the right
                end. Hidden while empty, so a panel with nothing to say leaves no gap to wrap. */}
            {zoneIndex === 0 && aside !== undefined && (
              <div className="ms-auto empty:hidden">{aside}</div>
            )}
          </div>
        </section>
      ))}
    </div>
  );
}

/**
 * The lockers whose state changed in the last refresh that changed any, and a round number that
 * goes up with each such refresh. A door keys its pulse on the round, so a door that changes
 * twice pulses twice. The first drawing changes nothing: every door is new to the eye then.
 *
 * Kept by comparing with the previous list during render (React's "adjusting state when a prop
 * changes"), not in an effect: the pulse starts in the same paint as the new colour. The list keeps
 * its identity between refreshes that change nothing (TanStack Query's structural sharing).
 */
function useChangedDoors(lockers: Locker[]): { ids: ReadonlySet<string>; round: number } {
  const [seen, setSeen] = useState(lockers);
  const [changed, setChanged] = useState<{ ids: ReadonlySet<string>; round: number }>({
    ids: new Set(),
    round: 0,
  });

  if (seen !== lockers) {
    const before = new Map(seen.map((locker) => [locker.id, lockerState(locker)]));
    const ids = new Set(
      lockers
        .filter((locker) => {
          const previous = before.get(locker.id);
          return previous !== undefined && previous !== lockerState(locker);
        })
        .map((locker) => locker.id),
    );
    setSeen(lockers);
    if (ids.size > 0) {
      setChanged((current) => ({ ids, round: current.round + 1 }));
    }
  }

  return changed;
}

function LockerDoor({
  number,
  locker,
  mode,
  highlighted,
  found,
  changedRound,
  birthday,
  stays,
  onSelect,
}: {
  number: number;
  locker: Locker | undefined;
  mode: "desk" | "pick";
  highlighted: boolean;
  /** Whether a name search matched this door's holder; `null` while nothing is searched for. */
  found: boolean | null;
  /** The refresh round in which this door changed, or `null` if it did not in the last one. */
  changedRound: number | null;
  /** Whether the holder's birthday is today: the door celebrates. */
  birthday: boolean;
  stays: ReadonlyMap<string, StayProgress> | undefined;
  onSelect: (locker: Locker) => void;
}) {
  // A locker the API did not send cannot be acted on. It should never happen — all 72 are seeded —
  // but a gap in the drawing would hide that it did.
  if (locker === undefined) {
    return (
      <span
        className={cn(
          doorSize,
          "flex items-center justify-center rounded-lg border-2 border-dashed text-sm text-muted-foreground",
        )}
        aria-label={`کمد ${toPersianDigits(number)}، نامعلوم`}
      >
        {toPersianDigits(number)}
      </span>
    );
  }

  const state = lockerState(locker);
  const holder = lockerHolderName(locker);
  // Any money the holder owes, whatever it is for: a plan, هوازی or the cafe (BUSINESS_RULES.md §6);
  // for a guest, the visit's unpaid cafe (§7 *Guest visit*).
  const owes = holder !== null && Number(locker.holderDebt) > 0;
  // The visit behind an occupied door, once the list of everyone inside has caught up with it.
  const stay = isHeld(state) ? stays?.get(locker.id) : undefined;
  const celebrates = birthday && holder !== null;
  const holderText = [
    holder,
    celebrates ? birthdayLabel : null,
    owes ? debtorLabel : null,
    stay?.isLong ? longStayLabel : null,
  ]
    .filter((part) => part !== null)
    .join("، ");
  const label = `کمد ${toPersianDigits(number)}، ${lockerStateLabel[state]}${holder === null ? "" : ` — ${holderText}`}`;

  return (
    <button
      type="button"
      aria-label={label}
      title={holder === null ? lockerStateLabel[state] : holderText}
      disabled={mode === "pick" && state !== "free"}
      data-state={state}
      data-highlighted={highlighted || undefined}
      data-found={found ?? undefined}
      data-changed={changedRound !== null || undefined}
      data-birthday={celebrates || undefined}
      onClick={() => onSelect(locker)}
      className={cn(
        doorSize,
        doorFace,
        doorMotion,
        doorStateClass[state],
        celebrates && "shadow-[0_0_18px_-4px_var(--party-pink)]",
        // Pointed at from the desk panel. The ring stays for anyone who has asked for less motion.
        // Ring and offset together reach 4px out, inside the zone's padding, so its scrolling
        // edge never cuts them off.
        highlighted &&
          "-translate-y-1 animate-pulse ring-2 ring-primary ring-offset-2 ring-offset-background motion-reduce:translate-y-0 motion-reduce:animate-none",
        found === true &&
          "-translate-y-1 ring-2 ring-success ring-offset-2 ring-offset-background motion-reduce:translate-y-0",
        found === false && "opacity-30",
      )}
    >
      {changedRound !== null && (
        <span
          key={changedRound}
          aria-hidden
          className={cn(
            "pointer-events-none absolute inset-0 animate-door-changed rounded-[inherit] ring-2 ring-inset motion-reduce:hidden",
            changedRingClass[state],
          )}
        />
      )}
      {celebrates && <Celebration />}
      {owes && <DebtorTag />}
      {stay !== undefined && <StayBar progress={stay} />}
      {/* Its own query container, so what is written inside follows the door's size, not the
          screen's. The door's size never follows what is written. A debtor's number and name
          sit a little lower, clear of the tag. `relative` keeps them above a birthday's confetti. */}
      <span
        className={cn(
          "@container relative flex size-full flex-col items-center justify-center gap-1 px-1",
          owes && "pt-3",
        )}
      >
        <span className="text-sm font-bold @min-[3.5rem]:text-base @min-[4.5rem]:text-xl">
          {toPersianDigits(number)}
        </span>
        {holder !== null && (
          <span
            dir="rtl"
            className="hidden w-full text-center text-[11px] leading-tight text-muted-foreground break-words @min-[3.5rem]:line-clamp-2"
          >
            {holder}
          </span>
        )}
        {state === "outOfService" && <Lock aria-hidden className="size-3.5" />}
      </span>
    </button>
  );
}

/**
 * A door in the usage view (BUSINESS_RULES.md §6 *Locker usage map*): shaded by how often it was
 * used against the most used locker, with its number and the count under it, or amber and dashed
 * when nobody used it. An out-of-service locker keeps its lock, so a zero on it explains itself.
 * It is a picture, not a button: no desk work happens in this view.
 */
function UsageDoor({
  number,
  outOfService,
  uses,
  mostUses,
}: {
  number: number;
  outOfService: boolean;
  uses: number;
  mostUses: number;
}) {
  const level = usageLevel(uses, mostUses);
  const usesText = uses === 0 ? unusedLabel : `${toPersianDigits(uses)} بار`;
  const label = [
    `کمد ${toPersianDigits(number)}`,
    uses === 0 ? unusedLabel : `${toPersianDigits(uses)} بار استفاده`,
    outOfService ? lockerStateLabel.outOfService : null,
  ]
    .filter((part) => part !== null)
    .join("، ");

  return (
    <div
      role="img"
      aria-label={label}
      title={label}
      data-usage-level={level}
      className={cn(doorSize, doorFace, usageDoorClass[level])}
    >
      <span className="@container flex size-full flex-col items-center justify-center gap-0.5 px-1">
        <span className="text-sm font-bold @min-[4.5rem]:text-xl">{toPersianDigits(number)}</span>
        <span
          dir="rtl"
          className="text-[10px] leading-tight whitespace-nowrap @min-[4.5rem]:text-xs"
        >
          {usesText}
        </span>
        {outOfService && <Lock aria-hidden className="size-3" />}
      </span>
    </div>
  );
}

/**
 * A small red tag hanging from the door's top edge, in its top-right corner (BUSINESS_RULES.md §6).
 * `end-0` is the right inside the map (`dir="ltr"`), and the one rounded corner is the one that
 * hangs free, bottom-left. A single Persian word reads correctly without a `dir` of its own.
 */
function DebtorTag() {
  return (
    <span
      aria-hidden
      className="pointer-events-none absolute end-0 top-0 rounded-es-md bg-destructive px-1.5 pb-0.5 text-[10px] leading-snug font-bold whitespace-nowrap text-destructive-foreground"
    >
      {debtorLabel}
    </span>
  );
}
