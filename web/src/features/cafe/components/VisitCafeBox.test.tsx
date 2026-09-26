import { fireEvent, screen, within } from "@testing-library/react";

import type { CurrentlyInside } from "@/features/attendance/api";
import { currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { cafePage, orderOnAccount, proteinShake, water } from "@/test/cafe";
import { json, mockApi, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { debtItem, memberDebt, reza, serviceChargeDebtItem } from "@/test/members";
import { renderApp } from "@/test/renderApp";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";

const visit = openVisit(reza.id);

/** Reza's cafe order, bought during this visit. */
const visitOrder = { ...orderOnAccount, attendanceId: visit.id };

describe("VisitCafeBox on the currently inside board", () => {
  it("Board_NothingBoughtYet_OffersToAddAPurchase", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
    });

    renderApp("/attendance", { session: session() });

    expect(await screen.findByRole("button", { name: "افزودن خرید بوفه" })).toBeInTheDocument();
  });

  it("Board_ApiOlderThanTheCafeColumn_StillShowsTheRow", async () => {
    // An API started before this change sends rows with no cafeOrders at all.
    const rowFromAnOlderApi: Partial<CurrentlyInside> = insideRow(reza.fullName, visit);
    delete rowFromAnOlderApi.cafeOrders;
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([rowFromAnOlderApi as CurrentlyInside]),
    });

    renderApp("/attendance", { session: session() });

    expect(await screen.findByRole("link", { name: reza.fullName })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "افزودن خرید بوفه" })).toBeInTheDocument();
  });

  it("Board_AddPurchase_PutsItOnTheAccountTiedToTheVisit", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
      "GET /api/cafe/products": () => cafePage([water, proteinShake]),
      "POST /api/cafe/orders": () => json(201, visitOrder),
    });

    renderApp("/attendance", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: "افزودن خرید بوفه" }));

    const dialog = await screen.findByRole("dialog");
    const drinks = await within(dialog).findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(within(drinks).getByRole("button", { name: /شیک پروتئین/ }));
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت به حساب عضو" }));

    // The box closes and says where the money went, for a screen reader too.
    expect(await screen.findByText("۱۲۰٬۰۰۰ تومان به حساب رضا احمدی ثبت شد.")).toBeInTheDocument();
    const [request] = api.requestsTo("POST", "/api/cafe/orders");
    expect(await request!.json()).toEqual({
      memberId: reza.id,
      attendanceId: visit.id,
      items: [{ productId: proteinShake.id, quantity: 1 }],
      // Nothing is taken at the board: it is on the account until check-out or later.
      payment: null,
    });
  });

  it("Board_VisitThatBought_ShowsTheTotalAndListsThePurchases", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit, { cafeOrders: [visitOrder] })]),
    });

    renderApp("/attendance", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: "بوفه: ۱۲۰٬۰۰۰ تومان" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("شیک پروتئین × ۱")).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: /^ثبت پرداخت برای/ })).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "خرید دیگر" })).toBeInTheDocument();
  });

  it("CheckOut_VisitThatBought_ListsThePurchasesAndTheDebtBySource", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit, { cafeOrders: [visitOrder] })]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () =>
        memberDebt([
          debtItem(),
          serviceChargeDebtItem(),
          {
            ...debtItem(),
            kind: "CafeOrder",
            id: visitOrder.id,
            planName: null,
            price: 120000,
            netPaid: 0,
            outstanding: 120000,
          },
        ]),
      "GET /api/cafe/orders": () => cafePage([visitOrder]),
    });

    renderApp("/attendance", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: "ثبت خروج" }));
    const dialog = await screen.findByRole("dialog");

    const purchases = await within(dialog).findByRole("region", { name: "خریدهای بوفه" });
    expect(purchases).toHaveTextContent("شیک پروتئین × ۱");
    const [request] = api.requestsTo("GET", "/api/cafe/orders");
    expect(new URL(request!.url).searchParams.get("AttendanceId")).toBe(visit.id);

    const bySource = await within(dialog).findByLabelText("بدهی به تفکیک");
    expect(bySource).toHaveTextContent("بدهی پلن۶۰۰٬۰۰۰ تومان");
    expect(bySource).toHaveTextContent("بدهی هوازی۱۰٬۰۰۰ تومان");
    expect(bySource).toHaveTextContent("بدهی بوفه۱۲۰٬۰۰۰ تومان");
  });
});
