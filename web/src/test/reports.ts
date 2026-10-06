import type {
  AttendanceReport,
  FinancialPeriod,
  FinancialReport,
  MembersReport,
  NeedsAttention,
  Receivables,
  SubscriptionsSnapshot,
  TopCafeProduct,
} from "@/features/dashboard/api";

import { json } from "./mockApi";

const zero = { received: 0, refunded: 0, net: 0 };

const noMethods: FinancialPeriod["byMethod"] = [
  { method: "Card", money: zero },
  { method: "BankTransfer", money: zero },
  { method: "Cash", money: zero },
];

function period(overrides: Partial<FinancialPeriod>): FinancialPeriod {
  return {
    from: "2026-09-23",
    to: "2026-10-04",
    revenue: zero,
    bySource: [
      { source: "Membership", money: zero, sold: 0, soldAmount: 0, soldPaid: 0, soldOwed: 0 },
      { source: "SingleSession", money: zero, sold: 0, soldAmount: 0, soldPaid: 0, soldOwed: 0 },
      { source: "Cardio", money: zero, sold: 0, soldAmount: 0, soldPaid: 0, soldOwed: 0 },
      { source: "Miscellaneous", money: zero, sold: 0, soldAmount: 0, soldPaid: 0, soldOwed: 0 },
      { source: "Analysis", money: zero, sold: 0, soldAmount: 0, soldPaid: 0, soldOwed: 0 },
      { source: "Cafe", money: zero, sold: 0, soldAmount: 0, soldPaid: 0, soldOwed: 0 },
    ],
    byMethod: noMethods,
    receivedByMethod: noMethods,
    shopAndAnalysisByMethod: noMethods,
    byStaff: [],
    sales: 0,
    salesPaid: 0,
    salesOwed: 0,
    expenses: 0,
    expensesByCategory: [],
    netProfit: 0,
    cafeGrossProfit: 0,
    ...overrides,
  };
}

/**
 * Revenue up 20٪ on the range before, expenses up too (bad news, in red), and a range before
 * with no cafe profit at all, where a percent would say nothing.
 */
export const financialReport: FinancialReport = {
  current: period({
    revenue: { received: 13000000, refunded: 1000000, net: 12000000 },
    // 12 plans and 4 single visits sold in the range; the cafe's orders are counted too, never shown.
    // The plans sold are worth more than was paid for them so far.
    bySource: [
      {
        source: "Membership",
        money: { received: 9000000, refunded: 0, net: 9000000 },
        sold: 12,
        soldAmount: 10800000,
        soldPaid: 9000000,
        soldOwed: 1800000,
      },
      {
        source: "SingleSession",
        money: { received: 600000, refunded: 0, net: 600000 },
        sold: 4,
        soldAmount: 600000,
        soldPaid: 600000,
        soldOwed: 0,
      },
      { source: "Cardio", money: zero, sold: 0, soldAmount: 0, soldPaid: 0, soldOwed: 0 },
      {
        source: "Miscellaneous",
        money: { received: 1200000, refunded: 0, net: 1200000 },
        sold: 3,
        soldAmount: 1200000,
        soldPaid: 1200000,
        soldOwed: 0,
      },
      { source: "Analysis", money: zero, sold: 0, soldAmount: 0, soldPaid: 0, soldOwed: 0 },
      {
        source: "Cafe",
        money: { received: 1200000, refunded: 0, net: 1200000 },
        sold: 30,
        soldAmount: 1200000,
        soldPaid: 1200000,
        soldOwed: 0,
      },
    ],
    byMethod: [
      { method: "Card", money: { received: 9000000, refunded: 0, net: 9000000 } },
      { method: "BankTransfer", money: zero },
      { method: "Cash", money: { received: 4000000, refunded: 1000000, net: 3000000 } },
    ],
    // The shop's 1,200,000 came by card: the gym's own card money is the rest.
    receivedByMethod: [
      { method: "Card", money: { received: 7800000, refunded: 0, net: 7800000 } },
      { method: "BankTransfer", money: zero },
      { method: "Cash", money: { received: 4000000, refunded: 1000000, net: 3000000 } },
    ],
    shopAndAnalysisByMethod: [
      { method: "Card", money: { received: 1200000, refunded: 0, net: 1200000 } },
      { method: "BankTransfer", money: zero },
      { method: "Cash", money: zero },
    ],
    byStaff: [
      {
        userId: "0199a000-0000-7000-8000-000000000002",
        fullName: "سارا رضایی",
        money: { received: 13000000, refunded: 1000000, net: 12000000 },
      },
    ],
    sales: 15000000,
    salesPaid: 13000000,
    salesOwed: 2000000,
    expenses: 5000000,
    expensesByCategory: [
      { categoryId: "0199a000-0000-7000-8000-0000000000a1", name: "اجاره", amount: 5000000 },
    ],
    netProfit: 7000000,
    cafeGrossProfit: 800000,
  }),
  previous: period({
    from: "2026-09-11",
    to: "2026-09-22",
    revenue: { received: 10000000, refunded: 0, net: 10000000 },
    expenses: 4000000,
    netProfit: 6000000,
    sales: 15000000,
    cafeGrossProfit: 0,
  }),
  days: [
    { date: "2026-10-03", revenue: 5000000, expenses: 0 },
    { date: "2026-10-04", revenue: 7000000, expenses: 5000000 },
  ],
};

export const attendanceReport: AttendanceReport = {
  from: "2026-09-23",
  to: "2026-10-04",
  visits: 40,
  previousVisits: 50,
  members: 12,
  days: [{ date: "2026-10-04", visits: 40 }],
  byWeekday: ["Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"].map(
    (weekday) => ({
      weekday: weekday as AttendanceReport["byWeekday"][number]["weekday"],
      hours: Array.from({ length: 24 }, (_, hour) =>
        weekday === "Sunday" && hour === 18 ? 40 : 0,
      ),
    }),
  ),
};

/**
 * Five plans ended: three renewed, one still waiting, so the rate is 3 of 4. Six new people came
 * for a single visit: two bought a plan, two still have time, so that rate is 2 of 4.
 */
export const membersReport: MembersReport = {
  from: "2026-09-23",
  to: "2026-10-04",
  ended: 5,
  renewed: 3,
  waiting: 1,
  newMembers: 2,
  trials: 6,
  trialsConverted: 2,
  trialsWaiting: 2,
  days: [{ date: "2026-10-01", ended: 5, renewed: 3, waiting: 1, newMembers: 2 }],
};

export const topCafeProducts: TopCafeProduct[] = [
  {
    productId: "0199a000-0000-7000-8000-0000000000c1",
    name: "آب معدنی",
    quantity: 30,
    amount: 600000,
  },
];

export const receivables: Receivables = {
  total: 3500000,
  upTo7Days: 1000000,
  from8To30Days: 500000,
  over30Days: 2000000,
};

export const subscriptionsSnapshot: SubscriptionsSnapshot = {
  today: "2026-10-04",
  active: 42,
  frozen: 3,
  expiringSoon: 6,
  lowSessions: 4,
};

export const ali = {
  memberId: "0199a000-0000-7000-8000-0000000000d1",
  fullName: "علی محمدی",
  phoneNumber: "+989121234567",
};

export const needsAttention: NeedsAttention = {
  today: "2026-10-04",
  runningOut: [{ ...ali, sessionsLeft: 0, endDate: "2026-10-06" }],
  left: [],
  absent: [],
  oldDebts: [{ ...ali, owed: 2000000, oldestSaleOn: "2026-08-20" }],
  oldDebtWithoutMember: 0,
  payablesDue: [],
};

/** Every report the dashboard asks for, answering with the figures above. */
export const reportHandlers = {
  "GET /api/reports/financial": () => json(200, financialReport),
  "GET /api/reports/attendance": () => json(200, attendanceReport),
  "GET /api/reports/members": () => json(200, membersReport),
  "GET /api/reports/cafe-products": () => json(200, topCafeProducts),
  "GET /api/reports/receivables": () => json(200, receivables),
  "GET /api/reports/subscriptions": () => json(200, subscriptionsSnapshot),
  "GET /api/reports/needs-attention": () => json(200, needsAttention),
};
