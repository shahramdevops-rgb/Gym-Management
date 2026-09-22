import type { Member, MemberDebt } from "@/features/members/api";

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
  debt: 0,
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
  debt: 0,
};

/** One page of GET /api/members. */
export function membersPage(items: Member[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}

/** GET /api/members/{id}/debt: the total and the items it is made of. */
export function memberDebt(items: MemberDebt["items"] = []): Response {
  return json(200, {
    total: items.reduce((sum, item) => sum + Number(item.outstanding), 0),
    items,
  });
}

/** One owed subscription in a debt breakdown. */
export function debtItem(overrides: Partial<MemberDebt["items"][number]> = {}) {
  return {
    subscriptionId: "0199a000-0000-7000-8000-0000000000b1",
    planName: "ماهانه",
    startDate: "2026-09-01",
    endDate: "2026-09-30",
    price: 900000,
    netPaid: 300000,
    outstanding: 600000,
    ...overrides,
  };
}

/** The query string the app sent, for asserting on Search, IsActive and Page. */
export function queryOf(request: Request): URLSearchParams {
  return new URL(request.url).searchParams;
}
