/**
 * Where the gym's 72 lockers stand (BUSINESS_RULES.md §6 *Where they stand*), so the map on screen
 * matches what the desk sees in front of the member.
 *
 * Every cabinet is two columns of three, numbered down a column and on to the next: 1, 2, 3 in the
 * first column, 4, 5, 6 in the second. So a cabinet is named by its first number and the rest
 * follow. Zones hold groups — a wall, or a cabinet standing on its own — and a group holds
 * cabinets left to right, the way they stand.
 *
 * This is the one place the gym's floor plan lives. A new cabinet is a change here and a migration
 * (§6), agreed with the Owner first.
 */

/** Lockers per cabinet column, top to bottom. */
export const lockersPerColumn = 3;

/** Columns per cabinet. */
export const columnsPerCabinet = 2;

export interface LockerGroup {
  /** The first number of each cabinet, left to right. */
  cabinets: number[];
}

export interface LockerZone {
  name: string;
  /** Drawn apart from each other: a wall, then a cabinet that stands on another wall. */
  groups: LockerGroup[];
}

export const lockerZones: LockerZone[] = [
  {
    name: "بیرون رختکن",
    groups: [{ cabinets: [1, 7, 13, 19, 25] }],
  },
  {
    name: "داخل رختکن",
    groups: [{ cabinets: [31, 37, 43, 49, 55, 61] }, { cabinets: [67] }],
  },
];

/** A cabinet's columns, left to right, each top to bottom: [[1, 2, 3], [4, 5, 6]]. */
export function cabinetColumns(first: number): number[][] {
  return Array.from({ length: columnsPerCabinet }, (_, column) =>
    Array.from({ length: lockersPerColumn }, (_, row) => first + column * lockersPerColumn + row),
  );
}

/** Every locker number on the map, in drawing order. */
export function mapLockerNumbers(): number[] {
  return lockerZones.flatMap((zone) =>
    zone.groups.flatMap((group) => group.cabinets.flatMap((first) => cabinetColumns(first).flat())),
  );
}
