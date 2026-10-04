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

function period(overrides: Partial<FinancialPeriod>): FinancialPeriod {
  return {
    from: "2026-09-23",
    to: "2026-10-04",
    revenue: zero,
    bySource: [
      { source: "Membership", money: zero },
      { source: "SingleSession", money: zero },
      { source: "Cardio", money: zero },
      { source: "Miscellaneous", money: zero },
      { source: "Analysis", money: zero },
      { source: "Cafe", money: zero },
    ],
    byMethod: [
      { method: "Card", money: zero },
      { method: "BankTransfer", money: zero },
      { method: "Cash", money: zero },
    ],
    byStaff: [],
    sales: 0,
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
    byMethod: [
      { method: "Card", money: { received: 9000000, refunded: 0, net: 9000000 } },
      { method: "BankTransfer", money: zero },
      { method: "Cash", money: { received: 4000000, refunded: 1000000, net: 3000000 } },
    ],
    byStaff: [
      {
        userId: "0199a000-0000-7000-8000-000000000002",
        fullName: "سارا رضایی",
        money: { received: 13000000, refunded: 1000000, net: 12000000 },
      },
    ],
    sales: 15000000,
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
  byWeekday: [
    "Saturday",
    "Sunday",
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
  ].map((weekday) => ({
    weekday: weekday as AttendanceReport["byWeekday"][number]["weekday"],
    hours: Array.from({ length: 24 }, (_, hour) => (weekday === "Sunday" && hour === 18 ? 40 : 0)),
  })),
};

/** Five plans ended: three renewed, one still waiting, so the rate is 3 of 4. */
export const membersReport: MembersReport = {
  from: "2026-09-23",
  to: "2026-10-04",
  ended: 5,
  renewed: 3,
  waiting: 1,
  newMembers: 2,
  days: [{ date: "2026-10-01", ended: 5, renewed: 3, waiting: 1, newMembers: 2 }],
};

export const topCafeProducts: TopCafeProduct[] = [
  { productId: "0199a000-0000-7000-8000-0000000000c1", name: "آب معدنی", quantity: 30, amount: 600000 },
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
