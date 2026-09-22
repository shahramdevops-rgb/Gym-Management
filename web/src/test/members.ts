import type { Member } from "@/features/members/api";

import { json } from "./mockApi";

export const reza: Member = {
  id: "0199a000-0000-7000-8000-0000000000a1",
  fullName: "رضا احمدی",
  phoneNumber: "+989121234567",
  notes: "عضو قدیمی",
  birthDate: "1991-08-03",
  isActive: true,
  version: 5,
  createdAt: "2026-09-18T06:30:00Z",
  updatedAt: null,
  hasUnpaidSubscription: false,
};

export const ali: Member = {
  id: "0199a000-0000-7000-8000-0000000000a2",
  fullName: "علی رضایی",
  phoneNumber: "+989351234567",
  notes: null,
  birthDate: null,
  isActive: false,
  version: 7,
  createdAt: "2026-09-17T08:00:00Z",
  updatedAt: null,
  hasUnpaidSubscription: false,
};

/** One page of GET /api/members. */
export function membersPage(items: Member[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}

/** The query string the app sent, for asserting on Search, IsActive and Page. */
export function queryOf(request: Request): URLSearchParams {
  return new URL(request.url).searchParams;
}
