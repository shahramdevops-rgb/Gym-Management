import type { Plan } from "@/features/plans/api";

import { json } from "./mockApi";

export const monthly12: Plan = {
  id: "0199a000-0000-7000-8000-0000000000b1",
  name: "یک ماهه ۱۲ جلسه",
  durationDays: 30,
  sessionCount: 12,
  price: 900000,
  isActive: true,
  version: 3,
  createdAt: "2026-09-18T06:30:00Z",
  updatedAt: null,
};

export const unlimitedQuarter: Plan = {
  id: "0199a000-0000-7000-8000-0000000000b2",
  name: "سه ماهه آزاد",
  durationDays: 90,
  sessionCount: null,
  price: 2500000.5,
  isActive: false,
  version: 4,
  createdAt: "2026-09-17T08:00:00Z",
  updatedAt: null,
};

/** One page of GET /api/plans. */
export function plansPage(items: Plan[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}
