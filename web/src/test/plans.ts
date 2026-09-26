import type { Plan } from "@/features/plans/api";

import { json } from "./mockApi";

export const monthly12: Plan = {
  id: "0199a000-0000-7000-8000-0000000000b1",
  name: "یک ماهه ۱۲ جلسه",
  durationDays: 30,
  sessionCount: 12,
  price: 900000,
  isActive: true,
  kind: "Membership",
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
  kind: "Membership",
  version: 4,
  createdAt: "2026-09-17T08:00:00Z",
  updatedAt: null,
};

/**
 * The one plan a single visit is sold from (BUSINESS_RULES.md §3). Always 1 day, 1 session — the
 * API refuses any other shape, so a fixture with another shape would be testing a state that
 * cannot exist.
 */
export const singleSession: Plan = {
  id: "0199a000-0000-7000-8000-0000000000b3",
  name: "تک‌جلسه‌ای",
  durationDays: 1,
  sessionCount: 1,
  price: 150000,
  isActive: true,
  kind: "SingleSession",
  version: 1,
  createdAt: "2026-09-25T08:00:00Z",
  updatedAt: null,
};

/** One page of GET /api/plans. */
export function plansPage(items: Plan[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}
