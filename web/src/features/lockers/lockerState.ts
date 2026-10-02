import type { Locker } from "./api";

export type LockerState = "free" | "occupied" | "guest" | "cardio" | "outOfService";

/** Each state's name, for a door's label, the counts above the map and the legend under it. */
export const lockerStateLabel: Record<LockerState, string> = {
  free: "آزاد",
  occupied: "اشغال",
  guest: "مهمان",
  cardio: "هوازی",
  outOfService: "خارج از سرویس",
};

/**
 * A locker's state as the map colours it and the page decides what a click opens. Occupancy is
 * the API's, derived from the open visit (BUSINESS_RULES.md §6); a held locker reads as occupied
 * even if it was also marked out of service, because the visit is what the desk must deal with.
 * A guest's locker is held too, in a colour of its own, so the desk sees that nobody paid for
 * that key (§7 *Guest visit*); so is a member's who came in only for هوازی, in yellow, so the desk
 * sees that no session was taken and the treadmill is still to be charged (§7 *Cardio-only visit*).
 */
export function lockerState(locker: Locker): LockerState {
  if (locker.occupiedByGuestName !== null) {
    return "guest";
  }
  if (locker.occupiedOnCardioOnly) {
    return "cardio";
  }
  if (locker.isOccupied) {
    return "occupied";
  }
  return locker.isOutOfService ? "outOfService" : "free";
}

/** Whether somebody holds the locker, a member or a guest: a click opens their visit. */
export function isHeld(state: LockerState): boolean {
  return state === "occupied" || state === "guest" || state === "cardio";
}

/** Whoever holds the locker, by name: the member's, or the guest's. `null` when nobody does. */
export function lockerHolderName(locker: Locker): string | null {
  return locker.occupiedByMemberFullName ?? locker.occupiedByGuestName;
}
