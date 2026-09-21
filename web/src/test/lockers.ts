import type { Locker } from "@/features/lockers/api";

import { json } from "./mockApi";

export const freeLocker: Locker = {
  id: "0199a000-0000-7000-8000-0000000000f1",
  number: 1,
  isOutOfService: false,
  isOccupied: false,
  version: 1,
  createdAt: "2026-09-18T06:00:00Z",
  updatedAt: null,
};

export const occupiedLocker: Locker = {
  id: "0199a000-0000-7000-8000-0000000000f2",
  number: 2,
  isOutOfService: false,
  isOccupied: true,
  version: 2,
  createdAt: "2026-09-18T06:00:00Z",
  updatedAt: null,
};

export const outOfServiceLocker: Locker = {
  id: "0199a000-0000-7000-8000-0000000000f3",
  number: 3,
  isOutOfService: true,
  isOccupied: false,
  version: 3,
  createdAt: "2026-09-18T06:00:00Z",
  updatedAt: "2026-09-18T09:00:00Z",
};

/** One page of GET /api/lockers. */
export function lockersPage(items: Locker[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}
