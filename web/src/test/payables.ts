import type { Payable } from "@/features/payables/api";

import { equipment } from "./expenses";
import { json } from "./mockApi";

const ownerId = "0199a000-0000-7000-8000-000000000001";

/** A cheque past its date and not marked: «سررسید گذشته», and it can be passed. */
export const overdueCheque: Payable = {
  id: "0199a000-0000-7000-8000-0000000000c1",
  kind: "Cheque",
  amount: 50000000,
  dueDate: "2026-09-01",
  payee: "فروشگاه تجهیزات",
  description: "تردمیل",
  categoryId: equipment.id,
  categoryName: equipment.name,
  installmentNumber: null,
  installmentCount: null,
  status: "Pending",
  registeredByUserId: ownerId,
  paidAt: null,
  paidByUserId: null,
  expenseId: null,
  cancelledAt: null,
  cancelReason: null,
  cancelledByUserId: null,
  version: 4,
  createdAt: "2026-08-01T08:00:00Z",
  updatedAt: null,
};

/** A cheque dated far ahead: pending, and not yet passable (§9: never before its date). */
export const futureCheque: Payable = {
  ...overdueCheque,
  id: "0199a000-0000-7000-8000-0000000000c2",
  amount: 30000000,
  dueDate: "2099-01-01",
  payee: "تعمیرگاه",
  description: "دوچرخه",
  version: 5,
};

/** An instalment dated far ahead: unlike a cheque, it can be paid already (§9). */
export const futureInstallment: Payable = {
  ...overdueCheque,
  id: "0199a000-0000-7000-8000-0000000000c5",
  kind: "Installment",
  amount: 5000000,
  dueDate: "2099-02-01",
  payee: "بانک ملت",
  description: "وام دستگاه",
  installmentNumber: 3,
  installmentCount: 12,
  version: 8,
};

export const passedCheque: Payable = {
  ...overdueCheque,
  id: "0199a000-0000-7000-8000-0000000000c3",
  dueDate: "2026-08-15",
  payee: "بانک‌دار",
  description: "پیش‌قسط",
  status: "Paid",
  paidAt: "2026-08-15T09:00:00Z",
  paidByUserId: ownerId,
  expenseId: "0199a000-0000-7000-8000-0000000000b3",
};

export const cancelledCheque: Payable = {
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

/** The list the API sends: the page, and what is pending whatever the filter. */
export function payablesPage(
  items: Payable[],
  totals: { cheques: number; installments: number } = { cheques: 0, installments: 0 },
): Response {
  return json(200, {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    pendingTotal: totals.cheques + totals.installments,
    pendingChequeTotal: totals.cheques,
    pendingInstallmentTotal: totals.installments,
  });
}
