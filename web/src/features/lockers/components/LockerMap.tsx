import type { CSSProperties, ReactNode } from "react";

import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { Locker } from "../api";
import { cabinetColumns, columnsPerCabinet, lockerZones } from "../layout";
import { lockerState, type LockerState } from "../lockerState";
import { longStayLabel, type StayProgress } from "../longStay";
import { StayBar } from "./StayBar";

/** Doors side by side in the widest zone (inside the changing room: seven cabinets of two). */
const widestRowDoors =
  Math.max(...lockerZones.map((zone) => zone.groups.flatMap((group) => group.cabinets).length)) *
  columnsPerCabinet;

/**
 * A door's width: as wide as the map lets the widest zone be, never smaller than a fingertip and
 * never larger than a real door would read. `100cqw` is the map's own width (it is the query
 * container); 12rem leaves room for the cabinet frames and the gaps between cabinets and groups.
 * Every door gets the same size whatever is written on it, so a long name can never grow one.
 */
const doorWidth = `clamp(2.75rem, calc((100cqw - 12rem) / ${widestRowDoors}), 6rem)`;

/** Fixed square doors, sized by `--door` on the map. */
const doorSize = "w-(--door) shrink-0 aspect-square";

const stateLabel: Record<LockerState, string> = {
  free: "آزاد",
  occupied: "اشغال",
  outOfService: "خارج از سرویس",
};

/** Written across the corner of a door whose holder owes money. */
const debtorLabel = "بدهکار";

const stateClass: Record<LockerState, string> = {
  free: "border-success bg-success/15 text-success hover:bg-success/30",
  occupied: "border-destructive bg-destructive/15 text-destructive hover:bg-destructive/25",
  outOfService:
    "border-muted-foreground/50 text-muted-foreground bg-[repeating-linear-gradient(45deg,var(--muted)_0_5px,transparent_5px_10px)]",
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
}

/**
 * The gym's lockers drawn the way they stand (BUSINESS_RULES.md §6 *Where they stand*): each a
 * door-shaped button, green when free, red when someone holds it (their name on hover), grey and
 * hatched when out of service. The desk clicks the locker it gives a member, the way a cinema seat
 * is booked.
 *
 * The cabinets are laid out left to right, as on the wall, even though the page is right-to-left:
 * `dir="ltr"` on each zone keeps locker 1 at the left end where it really is. No location words
 * are written on the map; the drawing already says where a locker is, and the zone names are
 * only for a screen reader.
 *
 * The doors grow with the screen so the whole width is used, and an occupied door carries its
 * holder's name once it is wide enough to read, cut to two lines inside the door. A holder who
 * owes money gets "بدهکار" across the door's top-left corner, and a thin bar along its bottom fills
 * over three hours of the visit.
 */
export function LockerMap({
  lockers,
  onSelect,
  mode = "desk",
  aside,
  highlightedLockerId = null,
  stays,
}: LockerMapProps) {
  const byNumber = new Map(lockers.map((locker) => [Number(locker.number), locker]));

  return (
    <div className="@container space-y-6" style={{ "--door": doorWidth } as CSSProperties}>
      <Legend lockers={lockers} />
      {lockerZones.map((zone, zoneIndex) => (
        <section key={zone.name} aria-label={zone.name}>
          <div dir="ltr" className="flex flex-wrap items-start gap-10 overflow-x-auto pb-1">
            {zone.groups.map((group) => (
              <div key={group.cabinets[0]} className="flex gap-2">
                {group.cabinets.map((first) => (
                  <div
                    key={first}
                    className="flex gap-1 rounded-md border bg-muted/40 p-1"
                    data-testid={`cabinet-${first}`}
                  >
                    {cabinetColumns(first).map((column) => (
                      <div key={column[0]} className="flex flex-col gap-1">
                        {column.map((number) => (
                          <LockerDoor
                            key={number}
                            number={number}
                            locker={byNumber.get(number)}
                            mode={mode}
                            highlighted={
                              highlightedLockerId !== null &&
                              byNumber.get(number)?.id === highlightedLockerId
                            }
                            stays={stays}
                            onSelect={onSelect}
                          />
                        ))}
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

function LockerDoor({
  number,
  locker,
  mode,
  highlighted,
  stays,
  onSelect,
}: {
  number: number;
  locker: Locker | undefined;
  mode: "desk" | "pick";
  highlighted: boolean;
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
          "flex items-center justify-center rounded-sm border-2 border-dashed text-sm text-muted-foreground",
        )}
        aria-label={`کمد ${toPersianDigits(number)}، نامعلوم`}
      >
        {toPersianDigits(number)}
      </span>
    );
  }

  const state = lockerState(locker);
  const holder = locker.occupiedByMemberFullName;
  // Any money the holder owes, whatever it is for: a plan, هوازی or the cafe (BUSINESS_RULES.md §6).
  const owes = holder !== null && Number(locker.occupiedByMemberDebt) > 0;
  // The visit behind an occupied door, once the list of everyone inside has caught up with it.
  const stay = state === "occupied" ? stays?.get(locker.id) : undefined;
  const holderText = [holder, owes ? debtorLabel : null, stay?.isLong ? longStayLabel : null]
    .filter((part) => part !== null)
    .join("، ");
  const label = `کمد ${toPersianDigits(number)}، ${stateLabel[state]}${holder === null ? "" : ` — ${holderText}`}`;

  return (
    <button
      type="button"
      aria-label={label}
      title={holder === null ? stateLabel[state] : holderText}
      disabled={mode === "pick" && state !== "free"}
      data-state={state}
      data-highlighted={highlighted || undefined}
      onClick={() => onSelect(locker)}
      className={cn(
        doorSize,
        "relative overflow-hidden rounded-sm border-2 transition-colors",
        "focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
        "disabled:cursor-not-allowed disabled:opacity-40",
        stateClass[state],
        // Pointed at from the desk panel. The ring stays for anyone who has asked for less motion.
        // Ring and offset together reach 4px out, inside the cabinet's 5px of frame, so the zone's
        // scrolling edge never cuts them off.
        highlighted &&
          "animate-pulse ring-2 ring-primary ring-offset-2 ring-offset-background motion-reduce:animate-none",
      )}
    >
      {owes && <DebtorRibbon />}
      {stay !== undefined && <StayBar progress={stay} />}
      {/* Its own query container, so what is written inside follows the door's size, not the
          screen's. The door's size never follows what is written. A debtor's number and name sit
          at the bottom, out of the ribbon's corner, a little off the frame. */}
      <span
        className={cn(
          "@container flex size-full flex-col items-center gap-1 px-2",
          owes ? "justify-end pb-2" : "justify-center",
        )}
      >
        <span className="text-sm font-bold @min-[4.5rem]:text-lg">{toPersianDigits(number)}</span>
        {holder !== null && (
          <span
            dir="rtl"
            className="hidden w-full text-center text-xs leading-tight font-medium break-words @min-[4.5rem]:line-clamp-2"
          >
            {holder}
          </span>
        )}
      </span>
    </button>
  );
}

/**
 * A small band across the door's top-left corner, at 45°: the strip between the lines x + y = 22px
 * and x + y = 42px, cut out of a 42px square with `clip-path`. Its two ends are therefore exactly
 * on the door's top and left edges, whatever the door's size (the smallest door is 44px), and it
 * never reaches past the frame or into the number and name, which sit at the bottom beside it.
 * The word is centred on the band's middle line (16px in from each edge) and turned the same way.
 *
 * No `dir` on the positioned elements: `start` has to mean the left, as it does inside the map
 * (`dir="ltr"`). A single Persian word reads correctly without it.
 */
function DebtorRibbon() {
  return (
    <span
      aria-hidden
      className="pointer-events-none absolute start-0 top-0 size-[42px] bg-warning [clip-path:polygon(22px_0,100%_0,0_100%,0_22px)]"
    >
      <span className="absolute start-[16px] top-[16px] -translate-x-1/2 -translate-y-1/2 -rotate-45 text-[11px] leading-none font-bold whitespace-nowrap text-warning-foreground">
        {debtorLabel}
      </span>
    </span>
  );
}

function Legend({ lockers }: { lockers: Locker[] }) {
  const counts: Record<LockerState, number> = { free: 0, occupied: 0, outOfService: 0 };
  for (const locker of lockers) {
    counts[lockerState(locker)] += 1;
  }

  return (
    <ul aria-label="راهنمای کمدها" className="flex flex-wrap gap-4 text-sm">
      {(["free", "occupied", "outOfService"] as const).map((state) => (
        <li key={state} className="flex items-center gap-2">
          <span
            aria-hidden
            className={cn("inline-block h-4 w-3 rounded-sm border-2", stateClass[state])}
          />
          {stateLabel[state]}: {toPersianDigits(counts[state])}
        </li>
      ))}
    </ul>
  );
}
