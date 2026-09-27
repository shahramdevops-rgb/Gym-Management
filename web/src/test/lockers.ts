import type { Locker } from "@/features/lockers/api";

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
    version: 1,
    createdAt: "2026-09-27T00:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

/** Locker `number` held by a member, as the API derives it from their open visit. */
export function heldLocker(number: number, memberId: string, fullName: string): Locker {
  return locker(number, {
    isOccupied: true,
    occupiedByMemberId: memberId,
    occupiedByMemberFullName: fullName,
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
