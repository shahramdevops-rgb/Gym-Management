import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess } from "../test/renderSite";

const plan = {
  id: "0199a000-0000-7000-8000-000000000401",
  price: 1_080_000,
  durationDays: 30,
  totalSessions: 12,
  usedSessions: 4,
  remainingSessions: 8,
  startDate: "2026-10-10",
  endDate: "2026-11-08",
  status: "Active",
  frozenSince: null,
  totalFrozenDays: 0,
  cancelledAt: null,
  isSingleSession: false,
  netPaid: 500_000,
  paymentStatus: "Partial",
};

const next = {
  ...plan,
  id: "0199a000-0000-7000-8000-000000000402",
  startDate: "2026-11-09",
  endDate: "2026-12-08",
  usedSessions: 0,
  remainingSessions: 12,
  status: "Upcoming",
  netPaid: 1_080_000,
  paymentStatus: "Paid",
};

function page(items: unknown[], totalCount = items.length) {
  return { items, page: 1, pageSize: 10, totalCount };
}

/** The home with these answers on top of empty ones, signed in as an active member. */
function renderHome(handlers: Record<string, (request: Request) => Response | Promise<Response>>) {
  const api = mockApi({
    ...emptyHome,
    "GET /api/site/me": () => json(200, siteAccess().me),
    ...handlers,
  });
  renderSite("/", { access: siteAccess() });
  return api;
}

async function cardOf(title: string) {
  const heading = await screen.findByRole("heading", { name: title });
  const card = heading.closest<HTMLElement>("[data-slot='card']");
  if (card === null) {
    throw new Error(`«${title}» is not inside a card`);
  }
  return within(card);
}

describe("MyPlanCard", () => {
  it("MyPlan_Current_ShowsSessionsLeftTheEndAndWhatIsPaid", async () => {
    renderHome({ "GET /api/site/me/plan": () => json(200, { current: plan, next: null }) });

    const card = await cardOf("اشتراک من");
    expect(await card.findByText("۱۲ جلسه - ۳۰ روزه")).toBeInTheDocument();
    expect(card.getByText("فعال")).toBeInTheDocument();
    expect(card.getByText("۸")).toBeInTheDocument();
    expect(card.getByText("۱۴۰۵/۰۸/۱۷")).toBeInTheDocument();
    expect(card.getByText("پرداخت جزئی")).toBeInTheDocument();
    expect(card.getByText(/پرداخت شده است/)).toHaveTextContent("۵۰۰٬۰۰۰");
    expect(card.queryByText(/پلن بعدی/)).not.toBeInTheDocument();
  });

  it("MyPlan_BoughtForLater_ShowsTheNextPlanAndItsStart", async () => {
    renderHome({ "GET /api/site/me/plan": () => json(200, { current: plan, next }) });

    const card = await cardOf("اشتراک من");
    expect(await card.findByText(/پلن بعدی/)).toHaveTextContent("۱۴۰۵/۰۸/۱۸");
  });

  it("MyPlan_Frozen_SaysSinceWhen", async () => {
    const frozen = { ...plan, status: "Frozen", frozenSince: "2026-10-25", paymentStatus: "Paid" };
    renderHome({ "GET /api/site/me/plan": () => json(200, { current: frozen, next: null }) });

    const card = await cardOf("اشتراک من");
    expect(await card.findByText("از ۱۴۰۵/۰۸/۰۳ فریز است.")).toBeInTheDocument();
    expect(card.queryByText(/پرداخت شده است/)).not.toBeInTheDocument();
  });

  it("MyPlan_None_SaysSo", async () => {
    renderHome({});

    const card = await cardOf("اشتراک من");
    expect(await card.findByText("اشتراک فعالی ندارید.")).toBeInTheDocument();
  });
});

describe("MyDebtCard", () => {
  it("MyDebt_Nothing_IsOneLine", async () => {
    renderHome({});

    const card = await cardOf("بدهی من");
    expect(await card.findByText("بدهی ندارید.")).toBeInTheDocument();
    expect(card.queryByRole("list")).not.toBeInTheDocument();
  });

  it("MyDebt_Owed_ShowsTheTotalAndEachItem", async () => {
    const debt = {
      total: 680_000,
      items: [
        {
          kind: "Subscription",
          id: plan.id,
          plan: { durationDays: 30, totalSessions: 12, isSingleSession: false },
          serviceKind: null,
          sale: null,
          startDate: "2026-10-10",
          endDate: "2026-11-08",
          price: 1_080_000,
          netPaid: 500_000,
          outstanding: 580_000,
          cafeItems: [],
        },
        {
          kind: "CafeOrder",
          id: "0199a000-0000-7000-8000-000000000501",
          plan: null,
          serviceKind: null,
          sale: null,
          startDate: "2026-10-28",
          endDate: null,
          price: 100_000,
          netPaid: 0,
          outstanding: 100_000,
          cafeItems: [
            { productName: "قهوه", unitPrice: 40_000, quantity: 2, lineTotal: 80_000 },
            { productName: "کیک", unitPrice: 20_000, quantity: 1, lineTotal: 20_000 },
          ],
        },
      ],
    };
    renderHome({ "GET /api/site/me/debt": () => json(200, debt) });

    const card = await cardOf("بدهی من");
    expect(await card.findByText("۶۸۰٬۰۰۰ تومان")).toBeInTheDocument();
    const items = card.getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent("اشتراک ۱۲ جلسه - ۳۰ روزه");
    expect(items[0]).toHaveTextContent("۵۸۰٬۰۰۰ تومان");
    expect(items[1]).toHaveTextContent("بوفه");
    expect(items[1]).toHaveTextContent("قهوه ×۲، کیک");
  });
});

describe("MyHistoryCard", () => {
  const visit = {
    id: "0199a000-0000-7000-8000-000000000601",
    checkedInAt: "2026-10-28T14:30:00Z",
    checkedOutAt: "2026-10-28T16:00:00Z",
    lockerNumber: 12,
    usesReservePlace: false,
    isCardioOnly: false,
    isBirthdayVisit: false,
    cancelledAt: null,
    autoClosedAt: null,
    charges: [
      {
        id: "0199a000-0000-7000-8000-000000000602",
        kind: "Cardio",
        description: null,
        quantity: null,
        unitPrice: null,
        amount: 50_000,
        netPaid: 0,
        paymentStatus: "Unpaid",
      },
    ],
  };

  it("MyHistory_Visits_ShowTheLockerTheCheckOutAndTheCharge", async () => {
    const cancelled = {
      ...visit,
      id: "0199a000-0000-7000-8000-000000000603",
      lockerNumber: null,
      checkedOutAt: null,
      cancelledAt: "2026-10-27T10:00:00Z",
      charges: [],
    };
    renderHome({ "GET /api/site/me/visits": () => json(200, page([visit, cancelled])) });

    const card = await cardOf("سوابق من");
    const rows = await card.findAllByRole("listitem");
    expect(rows[0]).toHaveTextContent("خروج ۱۹:۳۰ · کمد ۱۲");
    expect(rows[0]).toHaveTextContent("هوازی: ۵۰٬۰۰۰ تومان");
    expect(rows[0]).toHaveTextContent("پرداخت‌نشده");
    expect(rows[1]).toHaveTextContent("لغو شده");
  });

  it("MyHistory_Tabs_ReadEachHistoryOnlyWhenOpened", async () => {
    const refund = {
      id: "0199a000-0000-7000-8000-000000000701",
      targetKind: "Subscription",
      targetId: plan.id,
      subscriptionPlan: { durationDays: 30, totalSessions: 12, isSingleSession: false },
      serviceKind: null,
      serviceDescription: null,
      kind: "Refund",
      amount: 200_000,
      method: "Card",
      referenceNumber: null,
      paidAt: "2026-10-20T09:00:00Z",
      settlement: null,
    };
    const api = renderHome({ "GET /api/site/me/payments": () => json(200, page([refund])) });
    const card = await cardOf("سوابق من");
    await card.findByText("هنوز مراجعه‌ای ثبت نشده است.");
    expect(api.requestsTo("GET", "/api/site/me/payments")).toHaveLength(0);

    fireEvent.click(card.getByRole("tab", { name: "پرداخت‌ها" }));

    const row = await card.findByRole("listitem");
    expect(row).toHaveTextContent("اشتراک ۱۲ جلسه - ۳۰ روزه");
    expect(row).toHaveTextContent("بازگشت وجه");
    expect(row).toHaveTextContent("کارت");
    expect(row).toHaveTextContent("−۲۰۰٬۰۰۰ تومان");
    expect(api.requestsTo("GET", "/api/site/me/payments")).toHaveLength(1);
    expect(api.requestsTo("GET", "/api/site/me/plans")).toHaveLength(0);
  });

  it("MyHistory_CancelledCafePurchase_IsMarkedWithoutWhatIsOwed", async () => {
    const order = {
      id: "0199a000-0000-7000-8000-000000000801",
      totalAmount: 80_000,
      orderedOn: "2026-10-26",
      createdAt: "2026-10-26T12:00:00Z",
      cancelledAt: "2026-10-26T12:05:00Z",
      netPaid: 0,
      paymentStatus: "Unpaid",
      outstanding: 0,
      items: [{ productName: "قهوه", unitPrice: 40_000, quantity: 2, lineTotal: 80_000 }],
    };
    renderHome({ "GET /api/site/me/cafe-orders": () => json(200, page([order])) });
    const card = await cardOf("سوابق من");

    fireEvent.click(await card.findByRole("tab", { name: "بوفه" }));

    const row = await card.findByRole("listitem");
    expect(row).toHaveTextContent("لغو شده");
    expect(row).toHaveTextContent("قهوه ×۲");
    expect(row).not.toHaveTextContent("پرداخت‌نشده");
    expect(row).not.toHaveTextContent("مانده");
  });

  it("MyHistory_ManyPlans_PagerReadsTheNextPage", async () => {
    const api = renderHome({
      "GET /api/site/me/plans": (request) =>
        json(200, {
          ...page([new URL(request.url).searchParams.get("Page") === "2" ? next : plan], 11),
        }),
    });
    const card = await cardOf("سوابق من");
    fireEvent.click(await card.findByRole("tab", { name: "اشتراک‌ها" }));
    expect(await card.findByText("صفحهٔ ۱ از ۲")).toBeInTheDocument();

    fireEvent.click(card.getByRole("button", { name: "بعدی" }));

    expect(await card.findByText("در انتظار شروع")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/site/me/plans")).toHaveLength(2);
  });
});

describe("MyDetailsCard", () => {
  it("MyDetails_Shown_ReadOnlyWithWhereToCorrectThem", async () => {
    renderHome({});

    const card = await cardOf("مشخصات من");
    expect(await card.findByText("علی محمدی")).toBeInTheDocument();
    expect(card.getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toBeInTheDocument();
    expect(card.getByText("۱۳۷۴/۰۱/۰۱")).toBeInTheDocument();
    expect(card.getByText("برای اصلاح مشخصات به پذیرش باشگاه مراجعه کنید.")).toBeInTheDocument();
    expect(card.queryByRole("textbox")).not.toBeInTheDocument();
    expect(card.queryByRole("button")).not.toBeInTheDocument();
  });
});
