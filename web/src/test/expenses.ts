import type { Expense, ExpenseCategory } from "@/features/expenses/api";

import { json } from "./mockApi";

export const rent: ExpenseCategory = {
  id: "0199a000-0000-7000-8000-0000000000a1",
  name: "اجاره",
  version: 1,
  createdAt: "2026-09-27T00:00:00Z",
  updatedAt: null,
};

export const electricity: ExpenseCategory = {
  id: "0199a000-0000-7000-8000-0000000000a2",
  name: "برق",
  version: 2,
  createdAt: "2026-09-27T00:00:00Z",
  updatedAt: null,
};

export const septemberRent: Expense = {
  id: "0199a000-0000-7000-8000-0000000000b1",
  amount: 50000000,
  categoryId: rent.id,
  categoryName: rent.name,
  expenseDate: "2026-09-01",
  description: "اجارهٔ شهریور",
  referenceNumber: "TR-4412",
  recordedByUserId: "0199a000-0000-7000-8000-000000000001",
  isVoided: false,
  voidedAt: null,
  voidReason: null,
  voidedByUserId: null,
  version: 7,
  createdAt: "2026-09-01T08:00:00Z",
  updatedAt: null,
};

/** Entered twice by mistake: listed, marked, and never counted in the total (§9). */
export const voidedBill: Expense = {
  id: "0199a000-0000-7000-8000-0000000000b2",
  amount: 1200000,
  categoryId: electricity.id,
  categoryName: electricity.name,
  expenseDate: "2026-09-10",
  description: "قبض برق",
  referenceNumber: null,
  recordedByUserId: "0199a000-0000-7000-8000-000000000001",
  isVoided: true,
  voidedAt: "2026-09-11T09:00:00Z",
  voidReason: "دو بار ثبت شد",
  voidedByUserId: "0199a000-0000-7000-8000-000000000001",
  version: 3,
  createdAt: "2026-09-10T08:00:00Z",
  updatedAt: "2026-09-11T09:00:00Z",
};

export function categoriesPage(items: ExpenseCategory[]): Response {
  return json(200, { items, page: 1, pageSize: 100, totalCount: items.length });
}

/** The list the API sends: the page, and the total of every non-voided match across all pages. */
export function expensesPage(items: Expense[], totalAmount: number | string): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount: items.length, totalAmount });
}
