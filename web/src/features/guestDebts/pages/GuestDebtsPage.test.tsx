import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { GuestDebt } from "@/features/guestDebts/api";
import { json, mockApi, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { confirmMoneyReceived, pickMethod } from "@/test/payments";
import { renderApp } from "@/test/renderApp";

// «بدهی مهمان‌ها» (BUSINESS_RULES.md §7 *Guest visit*, task 6.5.31), as Staff: what guests' visits
// still owe, each row paid, voided or cancelled with the item's own forms.

/** A treadmill amount Maryam left unpaid when the nightly job closed her visit. */
const maryamCardio: GuestDebt = {
  target: "ServiceCharge",
  id: "0199a000-0000-7000-8000-00000000d0c1",
  attendanceId: "0199a000-0000-7000-8000-0000000000c9",
  guestName: "مریم احمدی",
  day: "2026-10-03",
  recordedAt: "2026-10-03T15:00:00Z",
  serviceKind: "Cardio",
  description: null,
  quantity: null,
  cafeItems: null,
  amount: 30000,
  netPaid: 10000,
  outstanding: 20000,
  paymentStatus: "Partial",
  visitIsOpen: false,
};

/** Two gloves Sara bought while still inside. */
const saraGloves: GuestDebt = {
  ...maryamCardio,
  id: "0199a000-0000-7000-8000-00000000d0c2",
  attendanceId: "0199a000-0000-7000-8000-0000000000ca",
  guestName: "سارا کریمی",
  serviceKind: "Miscellaneous",
  description: "دستکش",
  quantity: 2,
  amount: 300000,
  netPaid: 0,
  outstanding: 300000,
  paymentStatus: "Unpaid",
  visitIsOpen: true,
};

/** A drink of Maryam's from the same visit. */
const maryamDrink: GuestDebt = {
  ...maryamCardio,
  target: "CafeOrder",
  id: "0199a000-0000-7000-8000-00000000d0c3",
  serviceKind: null,
  cafeItems: [{ productName: "آب معدنی", quantity: 2 }],
  amount: 40000,
  netPaid: 0,
  outstanding: 40000,
  paymentStatus: "Unpaid",
};

function debtsPage(items: GuestDebt[]) {
  return json(200, { items, page: 1, pageSize: 20, totalCount: items.length });
}

function rowOf(text: string) {
  const row = screen.getByText(text).closest("tr");
  if (row === null) {
    throw new Error(`No row shows ${text}`);
  }
  return row;
}

describe("GuestDebtsPage", () => {
  it("Debts_Loaded_ShowsEachGuestWhatTheyBoughtAndWhatIsLeft", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/guest-debts": () => debtsPage([saraGloves, maryamCardio, maryamDrink]),
    });
    renderApp("/guest-debts", { session: session() });

    expect(screen.getByRole("heading", { name: "بدهی مهمان‌ها" })).toBeInTheDocument();
    await screen.findByText("هوازی");

    const gloves = rowOf("فروشگاه: دستکش × ۲");
    expect(gloves).toHaveTextContent("سارا کریمی");
    // Still inside: the box can settle it too.
    expect(gloves).toHaveTextContent("داخل باشگاه");
    expect(gloves).toHaveTextContent("۳۰۰٬۰۰۰");

    const cardio = rowOf("هوازی");
    expect(cardio).toHaveTextContent("مریم احمدی");
    expect(cardio).toHaveTextContent("مهمان");
    expect(cardio).toHaveTextContent("۲۰٬۰۰۰");
    expect(within(cardio).getByRole("button", { name: /^ابطال هوازی/ })).toBeInTheDocument();

    const drink = rowOf("بوفه: آب معدنی × ۲");
    expect(within(drink).getByRole("button", { name: /^لغو بوفه/ })).toBeInTheDocument();
  });

  it("Debts_None_SaysNothingIsLeft", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/guest-debts": () => debtsPage([]) });
    renderApp("/guest-debts", { session: session() });

    expect(
      await screen.findByText("بدهی پرداخت‌نشده‌ای از مهمان‌ها نمانده است."),
    ).toBeInTheDocument();
  });

  it("Debts_PayACharge_SendsItToTheChargeAndRefreshesTheList", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/guest-debts": () => debtsPage([maryamCardio]),
      [`POST /api/service-charges/${maryamCardio.id}/payments`]: () => json(201, { id: "payment" }),
    });
    renderApp("/guest-debts", { session: session() });

    await screen.findByText("هوازی");
    fireEvent.click(screen.getByRole("button", { name: /^ثبت پرداخت برای هوازی/ }));
    fireEvent.change(screen.getByLabelText("مبلغ (تومان)"), { target: { value: "20000" } });
    pickMethod(document.body);
    fireEvent.click(screen.getByRole("button", { name: "تأیید پرداخت" }));
    await confirmMoneyReceived();

    expect(await screen.findByRole("status")).toHaveTextContent("پرداخت ثبت شد.");
    const [request] = api.requestsTo("POST", `/api/service-charges/${maryamCardio.id}/payments`);
    expect(await request!.json()).toMatchObject({ amount: "20000", method: "Cash" });
    await waitFor(() => expect(api.requestsTo("GET", "/api/guest-debts")).toHaveLength(2));
  });

  it("Debts_CancelACafeOrder_AsksForAReasonAndSendsItToTheOrder", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/guest-debts": () => debtsPage([maryamDrink]),
      [`POST /api/cafe/orders/${maryamDrink.id}/cancel`]: () => json(200, { id: maryamDrink.id }),
    });
    renderApp("/guest-debts", { session: session() });

    await screen.findByText("بوفه: آب معدنی × ۲");
    fireEvent.click(screen.getByRole("button", { name: /^لغو بوفه/ }));
    fireEvent.change(screen.getByLabelText("دلیل لغو"), { target: { value: "مهمان برنگشت" } });
    fireEvent.click(screen.getByRole("button", { name: /تأیید لغو/ }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/cafe/orders/${maryamDrink.id}/cancel`)).toHaveLength(1),
    );
    const [request] = api.requestsTo("POST", `/api/cafe/orders/${maryamDrink.id}/cancel`);
    expect(await request!.json()).toEqual({ reason: "مهمان برنگشت" });
  });

  it("Debts_PageInTheUrl_IsSent", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/guest-debts": () =>
        json(200, { items: [maryamCardio], page: 2, pageSize: 20, totalCount: 21 }),
    });
    renderApp("/guest-debts?page=2", { session: session() });

    await screen.findByText("هوازی");
    const url = new URL(api.requestsTo("GET", "/api/guest-debts")[0]!.url);
    expect(url.searchParams.get("Page")).toBe("2");
  });
});
