import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { Locker } from "../api";
import { cabinetColumns, lockerZones } from "../layout";
import { lockerState, type LockerState } from "../lockerState";

const stateLabel: Record<LockerState, string> = {
  free: "آزاد",
  occupied: "اشغال",
  outOfService: "خارج از سرویس",
};

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
}

/**
 * The gym's lockers drawn the way they stand (BUSINESS_RULES.md §6 *Where they stand*): each a
 * door-shaped button, green when free, red when someone holds it (their name on hover), grey and
 * hatched when out of service. The desk clicks the locker it gives a member, the way a cinema seat
 * is booked.
 *
 * The cabinets are laid out left to right, as on the wall, even though the page is right-to-left:
 * `dir="ltr"` on each zone keeps locker 1 at the left end where it really is. No location words
 * are repeated on a locker; the drawing already says where it is.
 */
export function LockerMap({ lockers, onSelect, mode = "desk" }: LockerMapProps) {
  const byNumber = new Map(lockers.map((locker) => [Number(locker.number), locker]));

  return (
    <div className="space-y-5">
      <Legend lockers={lockers} />
      {lockerZones.map((zone) => (
        <section key={zone.name} aria-label={zone.name} className="space-y-2">
          <h3 className="text-sm font-medium text-muted-foreground">{zone.name}</h3>
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
                            onSelect={onSelect}
                          />
                        ))}
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            ))}
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
  onSelect,
}: {
  number: number;
  locker: Locker | undefined;
  mode: "desk" | "pick";
  onSelect: (locker: Locker) => void;
}) {
  // A locker the API did not send cannot be acted on. It should never happen — all 72 are seeded —
  // but a gap in the drawing would hide that it did.
  if (locker === undefined) {
    return (
      <span
        className="flex h-14 w-11 items-center justify-center rounded-sm border-2 border-dashed text-sm text-muted-foreground"
        aria-label={`کمد ${toPersianDigits(number)}، نامعلوم`}
      >
        {toPersianDigits(number)}
      </span>
    );
  }

  const state = lockerState(locker);
  const holder = locker.occupiedByMemberFullName;
  const label = `کمد ${toPersianDigits(number)}، ${stateLabel[state]}${holder === null ? "" : ` — ${holder}`}`;

  return (
    <button
      type="button"
      aria-label={label}
      title={holder ?? stateLabel[state]}
      disabled={mode === "pick" && state !== "free"}
      data-state={state}
      onClick={() => onSelect(locker)}
      className={cn(
        "relative flex h-14 w-11 items-center justify-center rounded-sm border-2 text-sm font-bold transition-colors",
        "focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
        "disabled:cursor-not-allowed disabled:opacity-40",
        stateClass[state],
      )}
    >
      {toPersianDigits(number)}
      {/* The handle, so it reads as a door and not a tile. */}
      <span
        aria-hidden
        className="absolute end-1 top-1/2 h-2 w-0.5 -translate-y-1/2 rounded bg-current"
      />
    </button>
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
