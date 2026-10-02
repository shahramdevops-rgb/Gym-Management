import type {
  HistoryAttendance,
  HistoryPayment,
  HistoryServiceCharge,
} from "@/features/history/api";

import { json } from "./mockApi";
import { ali, reza } from "./members";

/** One page of any history list. */
export function historyPage<T>(items: T[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}

export const closedVisit: HistoryAttendance = {
  id: "0199a000-0000-7000-8000-0000000000d1",
  memberId: reza.id,
  memberFullName: reza.fullName,
  guestName: null,
  lockerNumber: 12,
  usesReservePlace: false,
  isCardioOnly: false,
  checkedInAt: "2026-10-02T06:00:00Z",
  checkedOutAt: "2026-10-02T07:30:00Z",
  cancelledAt: null,
  autoClosedAt: null,
  checkedInByFullName: "سارا رضایی",
};

export const autoClosedVisit: HistoryAttendance = {
  ...closedVisit,
  id: "0199a000-0000-7000-8000-0000000000d2",
  memberId: ali.id,
  memberFullName: ali.fullName,
  lockerNumber: null,
  usesReservePlace: true,
  checkedOutAt: "2026-10-01T20:30:00Z",
  autoClosedAt: "2026-10-01T20:30:00Z",
  checkedInByFullName: "مدیر باشگاه",
};

export const cancelledVisit: HistoryAttendance = {
  ...closedVisit,
  id: "0199a000-0000-7000-8000-0000000000d3",
  checkedOutAt: "2026-10-02T06:05:00Z",
  cancelledAt: "2026-10-02T06:05:00Z",
};

/** A guest's visit (BUSINESS_RULES.md §7 Guest visit): a name, no member. */
export const guestVisit: HistoryAttendance = {
  ...closedVisit,
  id: "0199a000-0000-7000-8000-0000000000d4",
  memberId: null,
  memberFullName: null,
  guestName: "مریم احمدی",
  lockerNumber: 7,
};

export const planPayment: HistoryPayment = {
  id: "0199a000-0000-7000-8000-0000000000e1",
  source: "Subscription",
  targetId: "0199a000-0000-7000-8000-0000000000f1",
  memberId: reza.id,
  memberFullName: reza.fullName,
  guestName: null,
  subscriptionPlan: { durationDays: 30, totalSessions: 12, isSingleSession: false },
  serviceKind: null,
  kind: "Payment",
  amount: 900000,
  method: "Card",
  referenceNumber: null,
  reason: null,
  paidAt: "2026-10-02T06:10:00Z",
  targetUndone: true,
  receivedByFullName: "سارا رضایی",
  settlement: null,
};

export const planRefund: HistoryPayment = {
  ...planPayment,
  id: "0199a000-0000-7000-8000-0000000000e2",
  kind: "Refund",
  reason: "انصراف عضو",
  paidAt: "2026-10-02T08:00:00Z",
  receivedByFullName: "مدیر باشگاه",
};

export const walkInCafePayment: HistoryPayment = {
  ...planPayment,
  id: "0199a000-0000-7000-8000-0000000000e3",
  source: "CafeOrder",
  memberId: null,
  memberFullName: null,
  subscriptionPlan: null,
  amount: 30000,
  method: "Cash",
  targetUndone: false,
};

/** Paid at the till for a guest's order on their visit. */
export const guestCafePayment: HistoryPayment = {
  ...walkInCafePayment,
  id: "0199a000-0000-7000-8000-0000000000e4",
  guestName: "مریم احمدی",
};

export const liveCardio: HistoryServiceCharge = {
  id: "0199a000-0000-7000-8000-0000000000c1",
  memberId: reza.id,
  memberFullName: reza.fullName,
  attendanceId: closedVisit.id,
  kind: "Cardio",
  amount: 50000,
  chargedOn: "2026-10-02",
  createdAt: "2026-10-02T06:30:00Z",
  recordedByFullName: "سارا رضایی",
  voidedAt: null,
  voidReason: null,
  voidedByFullName: null,
  netPaid: 0,
  paymentStatus: "Unpaid",
};

export const voidedCardio: HistoryServiceCharge = {
  ...liveCardio,
  id: "0199a000-0000-7000-8000-0000000000c2",
  amount: 500000,
  voidedAt: "2026-10-02T06:20:00Z",
  voidReason: "صفر اضافه",
  voidedByFullName: "مدیر باشگاه",
};

/** The two rows of one «تسویه یکجا» (BUSINESS_RULES.md §5): a single visit and a drink, one transfer. */
const transferSettlement = {
  id: "0199a000-0000-7000-8000-0000000000a9",
  total: 240000,
  itemCount: 2,
};

export const settledVisitPayment: HistoryPayment = {
  ...planPayment,
  id: "0199a000-0000-7000-8000-0000000000e5",
  subscriptionPlan: { durationDays: 1, totalSessions: 1, isSingleSession: true },
  amount: 180000,
  method: "BankTransfer",
  paidAt: "2026-10-02T09:00:00Z",
  targetUndone: false,
  settlement: transferSettlement,
};

export const settledCafePayment: HistoryPayment = {
  ...settledVisitPayment,
  id: "0199a000-0000-7000-8000-0000000000e6",
  source: "CafeOrder",
  targetId: "0199a000-0000-7000-8000-0000000000f6",
  subscriptionPlan: null,
  amount: 60000,
};
