import type { Locker, LockerVisit } from "@/features/lockers/api";

import { json } from "./mockApi";

/** The id the fixtures give locker `number`, so a test can name the one it expects in a request. */
export function lockerId(number: number): string {
  return `0199a000-0000-7000-8000-${String(number).padStart(12, "0")}`;
}

/** Locker `number`, free and in service unless the overrides say otherwise. */
export function locker(number: number, overrides: Partial<Locker> = {}): Locker {
  return {
    id: lockerId(number),
    number,
    isOutOfService: false,
    isOccupied: false,
    occupiedByMemberId: null,
    occupiedByMemberFullName: null,
    occupiedByGuestName: null,
    occupiedOnCardioOnly: false,
    holderDebt: 0,
    version: 1,
    createdAt: "2026-09-27T00:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

/**
 * Locker `number` held by a member, as the API derives it from their open visit, with what they
 * owe (nothing unless given).
 */
export function heldLocker(number: number, memberId: string, fullName: string, debt = 0): Locker {
  return locker(number, {
    isOccupied: true,
    occupiedByMemberId: memberId,
    occupiedByMemberFullName: fullName,
    holderDebt: debt,
  });
}

/** Locker `number` held by a guest (BUSINESS_RULES.md §7 *Guest visit*), with what their visit owes the cafe. */
export function guestLocker(number: number, guestName: string, debt = 0): Locker {
  return locker(number, {
    isOccupied: true,
    occupiedByGuestName: guestName,
    holderDebt: debt,
  });
}

/**
 * Locker `number` held by a member who came in only for هوازی (BUSINESS_RULES.md §7 *Cardio-only
 * visit*): no session was consumed, and the map draws the door yellow.
 */
export function cardioLocker(number: number, memberId: string, fullName: string, debt = 0): Locker {
  return locker(number, {
    isOccupied: true,
    occupiedByMemberId: memberId,
    occupiedByMemberFullName: fullName,
    occupiedOnCardioOnly: true,
    holderDebt: debt,
  });
}

/** The gym's 72 lockers (BUSINESS_RULES.md §6), all free, with the ones given replacing theirs. */
export function allLockers(...changed: Locker[]): Locker[] {
  const byNumber = new Map(changed.map((one) => [Number(one.number), one]));

  return Array.from({ length: 72 }, (_, index) => byNumber.get(index + 1) ?? locker(index + 1));
}

/** One page of GET /api/lockers. */
export function lockersPage(items: Locker[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 100, totalCount });
}

/**
 * GET /api/lockers/usage: all 72 lockers with the uses given by number, every other one unused, over
 * the last `days` days up to 2026-09-30.
 */
export function lockerUsage(uses: Partial<Record<number, number>> = {}, days = 30): Response {
  const to = new Date("2026-09-30T00:00:00Z");
  const from = new Date(to.getTime() - (days - 1) * 86_400_000);

  return json(200, {
    from: from.toISOString().slice(0, 10),
    to: "2026-09-30",
    days,
    lockers: Array.from({ length: 72 }, (_, index) => ({
      lockerId: lockerId(index + 1),
      number: index + 1,
      uses: uses[index + 1] ?? 0,
    })),
  });
}

/** One visit on a locker today, closed at 10:30 in Tehran unless the overrides say otherwise. */
export function lockerVisit(overrides: Partial<LockerVisit> = {}): LockerVisit {
  return {
    attendanceId: "0199b000-0000-7000-8000-000000000001",
    memberId: "0199b000-0000-7000-8000-0000000000aa",
    memberFullName: "رضا احمدی",
    guestName: null,
    isCardioOnly: false,
    checkedInAt: "2026-09-28T05:30:00Z",
    checkedOutAt: "2026-09-28T07:00:00Z",
    cancelledAt: null,
    ...overrides,
  };
}
