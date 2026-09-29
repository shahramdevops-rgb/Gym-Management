import type { Locker } from "./api";

export type LockerState = "free" | "occupied" | "outOfService";

/** Each state's name, for a door's label, the counts above the map and the legend under it. */
export const lockerStateLabel: Record<LockerState, string> = {
  free: "آزاد",
  occupied: "اشغال",
  outOfService: "خارج از سرویس",
};

/**
 * A locker's state as the map colours it and the page decides what a click opens. Occupancy is
 * the API's, derived from the open visit (BUSINESS_RULES.md §6); a held locker reads as occupied
 * even if it was also marked out of service, because the visit is what the desk must deal with.
 */
export function lockerState(locker: Locker): LockerState {
  if (locker.isOccupied) {
    return "occupied";
  }
  return locker.isOutOfService ? "outOfService" : "free";
}
