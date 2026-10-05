import type { Cheque } from "@/features/cheques/api";

import { json } from "./mockApi";

const ownerId = "0199a000-0000-7000-8000-000000000001";

/** Past its date and not marked: «سررسید گذشته», and it can be passed. */
export const overdueCheque: Cheque = {
  id: "0199a000-0000-7000-8000-0000000000c1",
  amount: 50000000,
  dueDate: "2026-09-01",
  payee: "فروشگاه تجهیزات",
  description: "قسط اول تردمیل",
  status: "Pending",
  registeredByUserId: ownerId,
  passedAt: null,
  passedByUserId: null,
  cancelledAt: null,
  cancelReason: null,
  cancelledByUserId: null,
  version: 4,
  createdAt: "2026-08-01T08:00:00Z",
  updatedAt: null,
};

/** Dated far ahead: pending, and not yet passable (§9: never before its date). */
export const futureCheque: Cheque = {
  ...overdueCheque,
  id: "0199a000-0000-7000-8000-0000000000c2",
  amount: 30000000,
  dueDate: "2099-01-01",
  payee: "تعمیرگاه",
  description: "قسط دوم دوچرخه",
  version: 5,
};

export const passedCheque: Cheque = {
  ...overdueCheque,
  id: "0199a000-0000-7000-8000-0000000000c3",
  dueDate: "2026-08-15",
  payee: "بانک‌دار",
  description: "قسط صفر",
  status: "Passed",
  passedAt: "2026-08-15T09:00:00Z",
  passedByUserId: ownerId,
};

export const cancelledCheque: Cheque = {
  ...overdueCheque,
  id: "0199a000-0000-7000-8000-0000000000c4",
  dueDate: "2026-08-20",
  payee: "فروشنده",
  description: "پیش‌پرداخت",
  status: "Cancelled",
  cancelledAt: "2026-08-10T09:00:00Z",
  cancelReason: "از فروشنده پس گرفته شد",
  cancelledByUserId: ownerId,
};

/** The list the API sends: the page, and the total of every pending cheque whatever the tab. */
export function chequesPage(items: Cheque[], pendingTotal: number | string): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount: items.length, pendingTotal });
}
