import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { currentlyInsideRefetchMs, type CurrentlyInside } from "@/features/attendance/api";
import { currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { cafePage, orderOnAccount, proteinShake, water } from "@/test/cafe";
import { allLockers, heldLocker, lockerId, lockersPage } from "@/test/lockers";
import { json, mockApi, session, signedInHandlers, staffUser, type Handler } from "@/test/mockApi";
import { cafeDebtItem, debtItem, memberDebt, reza, serviceChargeDebtItem } from "@/test/members";
import { confirmMoneyReceived, pickMethod } from "@/test/payments";
import { renderApp } from "@/test/renderApp";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";

/** Reza inside on locker 2: the only place a visit's cafe purchases are rung up from. */
const visit = { ...openVisit(reza.id), lockerId: lockerId(2), lockerNumber: 2 };

/** Reza's cafe order, bought during this visit. */
const visitOrder = { ...orderOnAccount, attendanceId: visit.id };

function lockerHandlers(
  row: CurrentlyInside = insideRow(reza.fullName, visit),
  extra: Record<string, Handler> = {},
): Record<string, Handler> {
  return {
    ...signedInHandlers(staffUser),
    "GET /api/lockers": () => lockersPage(allLockers(heldLocker(2, reza.id, reza.fullName))),
    "GET /api/attendance/currently-inside": () => currentlyInsidePage([row]),
    [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    [`GET /api/members/${reza.id}/debt`]: () => memberDebt([]),
    "GET /api/cafe/orders": () => cafePage([]),
    ...extra,
  };
}

/** Opens the box of the occupied locker 2. */
async function openLocker() {
  fireEvent.click(await screen.findByRole("button", { name: /^کمد ۲،/ }));
  return screen.findByRole("dialog");
}

/**
 * The till is often on another computer, so the locker's box polls: what it rings up for a member
 * who is inside reaches «تسویه یکجا» without a reload. Time is faked only to skip the wait.
 */
describe("The locker's box while the till sells elsewhere", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  /** The debt answers with the plan only until `soldAtTheTill` is set, then with a drink too. */
  function debtThatGrows() {
    const state = { soldAtTheTill: false };
    const handler = () =>
      memberDebt(state.soldAtTheTill ? [debtItem(), cafeDebtItem()] : [debtItem()]);

    return { state, handler };
  }

  async function openSettlement() {
    renderApp("/", { session: session() });
    const box = await openLocker();
    fireEvent.click(await within(box).findByRole("button", { name: "تسویه یکجا" }));

    return within(box).findByRole("form", { name: "تسویه یکجا" });
  }

  it("Locker_OrderFromTheTillMeanwhile_ReachesTheSettlementWithoutAReload", async () => {
    const debt = debtThatGrows();
    mockApi(lockerHandlers(undefined, { [`GET /api/members/${reza.id}/debt`]: debt.handler }));

    const form = await openSettlement();
    expect(within(form).getAllByRole("checkbox")).toHaveLength(1);

    debt.state.soldAtTheTill = true;
    await vi.advanceTimersByTimeAsync(currentlyInsideRefetchMs);

    // It arrives ticked, like every item, and the total to collect follows it.
    await waitFor(() => expect(within(form).getAllByRole("checkbox")).toHaveLength(2));
    expect(within(form).getAllByRole("checkbox")[0]).toBeChecked();
    expect(form).toHaveTextContent("۶۳۰٬۰۰۰ تومان");
  });

  /** The desk agreed to a list; an order that lands while "was it received?" is open stays owed. */
  it("Settle_OrderArrivesWhileConfirming_PaysOnlyWhatWasConfirmed", async () => {
    const debt = debtThatGrows();
    const settlementsPath = `/api/members/${reza.id}/settlements`;
    const api = mockApi(
      lockerHandlers(undefined, {
        [`GET /api/members/${reza.id}/debt`]: debt.handler,
        [`POST ${settlementsPath}`]: () =>
          json(201, { amount: 600000, method: "Cash", remainingDebt: 30000, payments: [] }),
      }),
    );

    const form = await openSettlement();
    pickMethod(form, "Cash");
    fireEvent.click(within(form).getByRole("button", { name: "تأیید تسویه" }));
    await screen.findByRole("dialog", { name: "آیا پول دریافت شد؟" });

    debt.state.soldAtTheTill = true;
    await vi.advanceTimersByTimeAsync(currentlyInsideRefetchMs);
    // The form sits behind the open question, hidden from the accessibility tree meanwhile.
    await waitFor(() =>
      expect(within(form).getAllByRole("checkbox", { hidden: true })).toHaveLength(2),
    );
    await confirmMoneyReceived();

    await waitFor(() => expect(api.requestsTo("POST", settlementsPath)).toHaveLength(1));
    const body = await api.requestsTo("POST", settlementsPath)[0]!.json();
    expect(body.items).toEqual([
      { kind: "Subscription", id: debtItem().id, outstanding: debtItem().outstanding },
    ]);
  });
});

describe("VisitCafeBox in the locker's box", () => {
  it("Locker_NothingBoughtYet_OffersToAddAPurchase", async () => {
    mockApi(lockerHandlers());

    renderApp("/", { session: session() });
    const box = await openLocker();

    expect(within(box).getByRole("button", { name: "خرید بوفه" })).toBeInTheDocument();
  });

  it("Locker_ApiOlderThanTheCafeSlot_StillOffersAPurchase", async () => {
    // An API started before the cafe reached the visit sends rows with no cafeOrders at all.
    const rowFromAnOlderApi: Partial<CurrentlyInside> = insideRow(reza.fullName, visit);
    delete rowFromAnOlderApi.cafeOrders;
    mockApi(lockerHandlers(rowFromAnOlderApi as CurrentlyInside));

    renderApp("/", { session: session() });
    const box = await openLocker();

    expect(within(box).getByRole("button", { name: "خرید بوفه" })).toBeInTheDocument();
  });

  it("Locker_AddPurchase_PutsItOnTheAccountTiedToTheVisit", async () => {
    const api = mockApi(
      lockerHandlers(undefined, {
        "GET /api/cafe/products": () => cafePage([water, proteinShake]),
        "POST /api/cafe/orders": () => json(201, visitOrder),
      }),
    );

    renderApp("/", { session: session() });
    fireEvent.click(within(await openLocker()).getByRole("button", { name: "خرید بوفه" }));

    const dialog = await screen.findByRole("dialog", { name: /^بوفه/ });
    const drinks = await within(dialog).findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(within(drinks).getByRole("button", { name: /شیک پروتئین/ }));
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت به حساب عضو" }));

    // The box stays open on a success step listing what the server saved, so the desk can check it.
    expect(await within(dialog).findByText("خرید بوفه ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByText("به حساب رضا احمدی")).toBeInTheDocument();
    const saved = within(dialog).getByRole("list", { name: "اقلام ثبت‌شده" });
    expect(saved).toHaveTextContent("شیک پروتئین × ۱");
    expect(saved).toHaveTextContent("۱۲۰٬۰۰۰ تومان");

    fireEvent.click(within(dialog).getAllByRole("button", { name: "بستن" })[0]!);
    await waitFor(() => expect(dialog).not.toBeInTheDocument());

    const [request] = api.requestsTo("POST", "/api/cafe/orders");
    expect(await request!.json()).toEqual({
      memberId: reza.id,
      attendanceId: visit.id,
      items: [{ productId: proteinShake.id, quantity: 1 }],
      // Nothing is taken here: it is on the account until check-out or later.
      payment: null,
    });
  });

  it("Locker_VisitThatBought_ShowsTheTotalAndListsThePurchases", async () => {
    mockApi(
      lockerHandlers(insideRow(reza.fullName, visit, { cafeOrders: [visitOrder] }), {
        "GET /api/cafe/orders": () => cafePage([visitOrder]),
      }),
    );

    renderApp("/", { session: session() });
    const box = await openLocker();

    // The same purchases twice over: the slot's total, and the visit's list beside the debt.
    const purchases = await within(box).findByRole("region", { name: "خریدهای بوفه" });
    expect(purchases).toHaveTextContent("شیک پروتئین × ۱");

    fireEvent.click(within(box).getByRole("button", { name: "بوفه: ۱۲۰٬۰۰۰ تومان" }));
    const dialog = await screen.findByRole("dialog", { name: /^بوفه/ });
    expect(within(dialog).getByText("شیک پروتئین × ۱")).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: /^ثبت پرداخت برای/ })).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "خرید دیگر" })).toBeInTheDocument();
  });

  /**
   * The same product bought twice — at the locker and at the till — is one line with the
   * quantities added, and the debt below stops at its totals by source: the lines are right above.
   */
  it("Locker_SameProductInTwoOrders_ShowsOneLineAndNoItemizedDebt", async () => {
    const secondOrder = {
      ...visitOrder,
      id: "0199a000-0000-7000-8000-0000000000f9",
      totalAmount: 255000,
      outstanding: 255000,
      items: [
        {
          ...visitOrder.items[0]!,
          id: "0199a000-0000-7000-8000-0000000001f9",
          quantity: 2,
          lineTotal: 240000,
        },
        {
          id: "0199a000-0000-7000-8000-0000000001fa",
          productId: water.id,
          productName: water.name,
          unitPrice: 15000,
          quantity: 1,
          lineTotal: 15000,
        },
      ],
    };
    mockApi(
      lockerHandlers(insideRow(reza.fullName, visit, { cafeOrders: [visitOrder, secondOrder] }), {
        [`GET /api/members/${reza.id}/debt`]: () =>
          memberDebt([
            {
              ...debtItem(),
              kind: "CafeOrder",
              id: visitOrder.id,
              plan: null,
              price: 120000,
              netPaid: 0,
              outstanding: 120000,
            },
          ]),
        "GET /api/cafe/orders": () => cafePage([visitOrder, secondOrder]),
      }),
    );

    renderApp("/", { session: session() });
    const box = await openLocker();

    const purchases = await within(box).findByRole("region", { name: "خریدهای بوفه" });
    const lines = within(purchases).getAllByRole("listitem");
    expect(lines).toHaveLength(2);
    expect(lines[0]).toHaveTextContent("شیک پروتئین × ۳");
    expect(lines[0]).toHaveTextContent("۳۶۰٬۰۰۰ تومان");
    expect(lines[1]).toHaveTextContent(`${water.name} × ۱`);

    expect(await within(box).findByLabelText("بدهی به تفکیک")).toBeInTheDocument();
    expect(within(box).queryByRole("list", { name: "بدهی جزء به جزء" })).not.toBeInTheDocument();
  });

  it("CheckOut_VisitThatBought_ListsThePurchasesAndTheDebtBySource", async () => {
    const api = mockApi(
      lockerHandlers(insideRow(reza.fullName, visit, { cafeOrders: [visitOrder] }), {
        [`GET /api/members/${reza.id}/debt`]: () =>
          memberDebt([
            debtItem(),
            serviceChargeDebtItem(),
            {
              ...debtItem(),
              kind: "CafeOrder",
              id: visitOrder.id,
              plan: null,
              price: 120000,
              netPaid: 0,
              outstanding: 120000,
            },
          ]),
        "GET /api/cafe/orders": () => cafePage([visitOrder]),
      }),
    );

    renderApp("/", { session: session() });
    fireEvent.click(within(await openLocker()).getByRole("button", { name: "ثبت خروج" }));
    const dialog = await screen.findByRole("dialog", { name: "ثبت خروج" });

    const purchases = await within(dialog).findByRole("region", { name: "خریدهای بوفه" });
    expect(purchases).toHaveTextContent("شیک پروتئین × ۱");
    const request = api.requestsTo("GET", "/api/cafe/orders").at(-1);
    expect(new URL(request!.url).searchParams.get("AttendanceId")).toBe(visit.id);

    const bySource = await within(dialog).findByLabelText("بدهی به تفکیک");
    expect(bySource).toHaveTextContent("بدهی پلن۶۰۰٬۰۰۰ تومان");
    expect(bySource).toHaveTextContent("بدهی هوازی۱۰٬۰۰۰ تومان");
    expect(bySource).toHaveTextContent("بدهی بوفه۱۲۰٬۰۰۰ تومان");
  });
});
