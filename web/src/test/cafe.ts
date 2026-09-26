import type { CafeOrder, Product, ProductCategory } from "@/features/cafe/api";

import { json } from "./mockApi";
import { reza } from "./members";

export const drinks: ProductCategory = {
  id: "0199a000-0000-7000-8000-0000000000c1",
  name: "نوشیدنی",
  isActive: true,
  version: 1,
  createdAt: "2026-09-25T08:00:00Z",
  updatedAt: null,
};

export const snacks: ProductCategory = {
  id: "0199a000-0000-7000-8000-0000000000c2",
  name: "تنقلات",
  isActive: false,
  version: 3,
  createdAt: "2026-09-25T08:00:00Z",
  updatedAt: null,
};

export const water: Product = {
  id: "0199a000-0000-7000-8000-0000000000e1",
  name: "آب معدنی",
  categoryId: drinks.id,
  categoryName: drinks.name,
  price: 25000,
  isActive: true,
  categoryIsActive: true,
  isSellable: true,
  version: 2,
  createdAt: "2026-09-25T08:00:00Z",
  updatedAt: null,
};

export const proteinShake: Product = {
  id: "0199a000-0000-7000-8000-0000000000e2",
  name: "شیک پروتئین",
  categoryId: drinks.id,
  categoryName: drinks.name,
  price: 120000,
  isActive: true,
  categoryIsActive: true,
  isSellable: true,
  version: 4,
  createdAt: "2026-09-25T08:00:00Z",
  updatedAt: null,
};

/** Switched on, but its category is not: the management list shows it, the till does not. */
export const chips: Product = {
  id: "0199a000-0000-7000-8000-0000000000e3",
  name: "چیپس",
  categoryId: snacks.id,
  categoryName: snacks.name,
  price: 40000,
  isActive: true,
  categoryIsActive: false,
  isSellable: false,
  version: 1,
  createdAt: "2026-09-25T08:00:00Z",
  updatedAt: null,
};

/** A walk-in sale, paid in full at the till. */
export const walkInOrder: CafeOrder = {
  id: "0199a000-0000-7000-8000-0000000000f1",
  memberId: null,
  memberFullName: null,
  attendanceId: null,
  totalAmount: 50000,
  orderedOn: "2026-09-26",
  placedByUserId: "0199a000-0000-7000-8000-000000000002",
  cancelledAt: null,
  cancelReason: null,
  netPaid: 50000,
  paymentStatus: "Paid",
  outstanding: 0,
  version: 1,
  createdAt: "2026-09-26T09:00:00Z",
  items: [
    {
      id: "0199a000-0000-7000-8000-0000000001f1",
      productId: water.id,
      productName: water.name,
      unitPrice: 25000,
      quantity: 2,
      lineTotal: 50000,
    },
  ],
};

/** On Reza's account, nothing paid yet. */
export const orderOnAccount: CafeOrder = {
  id: "0199a000-0000-7000-8000-0000000000f2",
  memberId: reza.id,
  memberFullName: reza.fullName,
  attendanceId: null,
  totalAmount: 120000,
  orderedOn: "2026-09-25",
  placedByUserId: "0199a000-0000-7000-8000-000000000002",
  cancelledAt: null,
  cancelReason: null,
  netPaid: 0,
  paymentStatus: "Unpaid",
  outstanding: 120000,
  version: 1,
  createdAt: "2026-09-25T15:00:00Z",
  items: [
    {
      id: "0199a000-0000-7000-8000-0000000001f2",
      productId: proteinShake.id,
      productName: proteinShake.name,
      unitPrice: 120000,
      quantity: 1,
      lineTotal: 120000,
    },
  ],
};

export const cancelledOrder: CafeOrder = {
  ...walkInOrder,
  id: "0199a000-0000-7000-8000-0000000000f3",
  cancelledAt: "2026-09-26T09:05:00Z",
  cancelReason: "اشتباه در ثبت",
  netPaid: 0,
  paymentStatus: "Unpaid",
  outstanding: 0,
  version: 2,
};

/** One page of any cafe list: categories, products or orders. */
export function cafePage<T>(items: T[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}
