import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import {
  cardioCharge,
  currentlyInsidePage,
  guestInsideRow,
  guestVisit,
  insideRow,
  openVisit,
} from "@/test/attendance";
import { orderOnAccount, water } from "@/test/cafe";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { cafeDebtItem, debtItem, memberDebt, reza, serviceChargeDebtItem } from "@/test/members";
import { confirmMoneyReceived, pickMethod } from "@/test/payments";
import { renderApp } from "@/test/renderApp";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";
import { gymToday } from "@/lib/format";

/**
 * An end date a number of days from the gym's today, counted the way the board counts it.
 * Built from `gymToday()` rather than from `Date.now()`: the board measures against the gym's
 * time zone, so a UTC-derived date made the expiry tests fail between midnight and 03:30 local
 * time, when UTC is still on the previous day.
 */
function endDateInDays(days: number): string {
  const date = new Date(`${gymToday()}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);

  return date.toISOString().slice(0, 10);
}

describe("CurrentlyInsidePage", () => {
  it("Board_SomeoneInside_ShowsNameLockerAndTime", async () => {
    const visit = openVisit(reza.id);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(row).toHaveTextContent("۳");
  });

  /** BUSINESS_RULES.md §7 *Cardio-only visit*: no session came from the plan beside it. */
  it("Board_CardioOnlyVisit_MarksTheRowAndOnlyThatRow", async () => {
    const cardio = { ...openVisit(reza.id), isCardioOnly: true };
    const ordinary = { ...openVisit("0199a000-0000-7000-8000-0000000000b9"), id: "other" };
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, cardio), insideRow("علی کریمی", ordinary)]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(within(row).getByText("فقط هوازی")).toBeInTheDocument();
    const other = screen.getByRole("link", { name: "علی کریمی" }).closest("tr")!;
    expect(within(other).queryByText("فقط هوازی")).not.toBeInTheDocument();
  });

  it("Board_LimitedSubscription_ShowsSessionsUsedOfTotal", async () => {
    const visit = openVisit(reza.id);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([
          insideRow(reza.fullName, visit, {
            totalSessions: 12,
            usedSessions: 4,
            remainingSessions: 8,
          }),
        ]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(within(row).getByText("۴ از ۱۲")).toBeInTheDocument();
    expect(within(row).getByRole("progressbar")).toHaveAttribute("aria-valuenow", "4");
  });

  /** BUSINESS_RULES.md §7: three or fewer sessions left is the desk's cue to mention renewing. */
  it("Board_ThreeSessionsLeft_MarksTheRowForAttention", async () => {
    const visit = openVisit(reza.id);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([
          insideRow(reza.fullName, visit, {
            totalSessions: 12,
            usedSessions: 9,
            remainingSessions: 3,
          }),
        ]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(within(row).getByText("۹ از ۱۲")).toHaveClass("text-warning");
  });

  /** BUSINESS_RULES.md §7: within five days of expiry, and how many days are left. */
  it("Board_SubscriptionExpiringWithinFiveDays_ShowsTheDaysLeft", async () => {
    const visit = openVisit(reza.id);
    const inThreeDays = endDateInDays(3);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([
          insideRow(reza.fullName, visit, { subscriptionEndDate: inThreeDays }),
        ]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(within(row).getByText("(۳ روز)")).toBeInTheDocument();
  });

  it("Board_SingleSessionVisit_ShowsItInsteadOfASessionBar", async () => {
    // BUSINESS_RULES.md §4, §7: "۱ از ۱" on every such row is a denominator with nothing to say.
    const visit = openVisit(reza.id);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([
          insideRow(reza.fullName, visit, {
            isSingleSession: true,
            totalSessions: 1,
            usedSessions: 1,
            remainingSessions: 0,
            subscriptionEndDate: gymToday(),
          }),
        ]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(within(row).getByText("تک‌جلسه‌ای")).toBeInTheDocument();
    expect(within(row).queryByText("۱ از ۱")).not.toBeInTheDocument();
  });

  it("Board_SingleSessionVisit_IsNotMarkedAsRunningOut", async () => {
    // It is spent by design and expires tonight, so the marks would fire on every row and mean
    // nothing. The row says what it is instead.
    const visit = openVisit(reza.id);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([
          insideRow(reza.fullName, visit, {
            isSingleSession: true,
            totalSessions: 1,
            usedSessions: 1,
            remainingSessions: 0,
            subscriptionEndDate: gymToday(),
          }),
        ]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(within(row).queryByText("(امروز)")).not.toBeInTheDocument();
  });

  it("Board_SubscriptionNotExpiringSoon_ShowsNoDaysLeft", async () => {
    const visit = openVisit(reza.id);
    const inTwoMonths = endDateInDays(60);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([
          insideRow(reza.fullName, visit, { subscriptionEndDate: inTwoMonths }),
        ]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(within(row).queryByText(/روز\)/)).not.toBeInTheDocument();
  });

  /**
   * BUSINESS_RULES.md §7 *The "currently inside" board*: هوازی and the cafe are rung up from the
   * member's locker (and the cafe from its till), so the board offers neither.
   */
  it("Board_VisitThatBought_OffersNoPurchase", async () => {
    const visit = openVisit(reza.id);
    const charged = { ...visit, serviceCharges: [cardioCharge(visit)] };
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, charged)]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(screen.queryByRole("columnheader", { name: "هوازی" })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "بوفه" })).not.toBeInTheDocument();
    expect(within(row).queryByRole("button", { name: "مبلغ هوازی" })).not.toBeInTheDocument();
    expect(within(row).queryByRole("button", { name: "خرید بوفه" })).not.toBeInTheDocument();
  });

  it("Board_NobodyInside_SaysSo", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () => currentlyInsidePage([]),
    });

    renderApp("/attendance", { session: session() });

    expect(await screen.findByText("در حال حاضر کسی داخل باشگاه نیست.")).toBeInTheDocument();
  });

  /** What the check-out box reads besides the check-out itself: the plan and the debt. */
  const boxHandlers = {
    [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    [`GET /api/members/${reza.id}/debt`]: () => memberDebt([]),
  };

  async function confirmCheckOut() {
    fireEvent.click(await screen.findByRole("button", { name: "ثبت خروج" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByLabelText("کلید کمد شماره ۳ را تحویل گرفتم"));
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" }));
    return dialog;
  }

  it("Board_CheckOut_AsksFirstAndSendsNothingUntilConfirmed", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      ...boxHandlers,
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
    });

    renderApp("/attendance", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: "ثبت خروج" }));

    // The same box as the entry screen (BUSINESS_RULES.md §7 Confirming at the front desk).
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("آیا از ثبت خروج رضا احمدی مطمئن هستید؟");
    expect(within(dialog).getByText("کلید کمد را از عضو تحویل بگیرید")).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" })).toBeDisabled();
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/check-out`)).toHaveLength(0);
  });

  it("Board_CheckOut_CallsTheApiAndRefreshesTheBoard", async () => {
    const visit = openVisit(reza.id);
    let stillInside = true;
    const api = mockApi({
      ...signedInHandlers(staffUser),
      ...boxHandlers,
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage(stillInside ? [insideRow(reza.fullName, visit)] : []),
      [`POST /api/attendance/${visit.id}/check-out`]: () => {
        stillInside = false;
        return json(200, { ...visit, checkedOutAt: "2026-09-18T09:00:00Z" });
      },
    });

    renderApp("/attendance", { session: session() });
    const dialog = await confirmCheckOut();

    expect(await within(dialog).findByText("خروج ثبت شد")).toBeInTheDocument();
    expect(
      await screen.findByText("در حال حاضر کسی داخل باشگاه نیست.", undefined, { timeout: 3000 }),
    ).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/check-out`)).toHaveLength(1);
  });

  /**
   * The case task 7.5 is for: the walk-in owes the visit, هوازی and a drink, and pays all of it
   * once, in the check-out box, before the check-out itself (BUSINESS_RULES.md §5).
   */
  it("Board_CheckOutBox_SettlesTheWholeDebtInOneStep", async () => {
    const visit = openVisit(reza.id);
    let paid = false;
    const api = mockApi({
      ...signedInHandlers(staffUser),
      ...boxHandlers,
      [`GET /api/members/${reza.id}/debt`]: () =>
        memberDebt(paid ? [] : [debtItem(), serviceChargeDebtItem(), cafeDebtItem()]),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
      [`POST /api/members/${reza.id}/settlements`]: () => {
        paid = true;
        return json(200, {
          amount: 640000,
          method: "Cash",
          payments: [
            {
              paymentId: "p1",
              kind: "CafeOrder",
              targetId: cafeDebtItem().id,
              amount: 30000,
              outstanding: 0,
            },
            {
              paymentId: "p2",
              kind: "ServiceCharge",
              targetId: serviceChargeDebtItem().id,
              amount: 10000,
              outstanding: 0,
            },
            {
              paymentId: "p3",
              kind: "Subscription",
              targetId: debtItem().id,
              amount: 600000,
              outstanding: 0,
            },
          ],
          remainingDebt: 0,
        });
      },
    });

    renderApp("/attendance", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: "ثبت خروج" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(await within(dialog).findByRole("button", { name: "تسویه یکجا" }));
    pickMethod(dialog);
    fireEvent.click(within(dialog).getByRole("button", { name: "تأیید تسویه" }));
    await confirmMoneyReceived();

    expect(await within(dialog).findByText("بدهی این عضو صاف شد.")).toBeInTheDocument();
    expect(await within(dialog).findByText("این عضو بدهی ندارد.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${reza.id}/settlements`)).toHaveLength(1);
    // Paying is not leaving: the check-out still waits for its own confirmation.
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/check-out`)).toHaveLength(0);
  });

  it("Board_CancelCheckIn_AsksFirstAndSendsNothingUntilConfirmed", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
    });

    renderApp("/attendance", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: "لغو ورود" }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("آیا از لغو ورود رضا احمدی مطمئن هستید؟");
    fireEvent.click(within(dialog).getByRole("button", { name: "انصراف" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`)).toHaveLength(0);
  });

  it("Board_CancelCheckIn_Confirmed_CancelsAndSaysTheSessionIsBack", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
      [`POST /api/attendance/${visit.id}/cancel`]: () =>
        json(200, {
          ...visit,
          checkedOutAt: "2026-09-18T07:05:00Z",
          cancelledAt: "2026-09-18T07:05:00Z",
        }),
    });

    renderApp("/attendance", { session: session() });
    const dialog = await openCancelBox();
    // Nothing bought: one question, as before 6.5.8, and nothing to tick.
    expect(within(dialog).queryByRole("checkbox")).not.toBeInTheDocument();
    fireEvent.click(await confirmCancelButton(dialog));

    expect(await within(dialog).findByText("ورود لغو شد")).toBeInTheDocument();
    expect(dialog).toHaveTextContent("جلسه به اشتراک بازگشت");
    const request = api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`).at(0)!;
    expect(await request.json()).toEqual({ voidCardio: false, cafeOrderIds: [] });
  });

  // ---- Cancel check-in with purchases (BUSINESS_RULES.md §7 Cancel check-in, roadmap 6.5.8) ----

  const visitCafeOrder = {
    ...orderOnAccount,
    id: "0199a000-0000-7000-8000-0000000000c1",
    attendanceId: openVisit(reza.id).id,
  };
  const secondCafeOrder = {
    ...visitCafeOrder,
    id: "0199a000-0000-7000-8000-0000000000c2",
    totalAmount: 25000,
    netPaid: 25000,
    items: [
      {
        id: "0199a000-0000-7000-8000-0000000001c2",
        productId: water.id,
        productName: water.name,
        unitPrice: 25000,
        quantity: 1,
        lineTotal: 25000,
      },
    ],
  };

  /** A visit with هوازی (10,000, 4,000 of it paid) and two cafe orders. */
  function visitWithPurchases() {
    const visit = openVisit(reza.id);
    const charged = { ...visit, serviceCharges: [cardioCharge(visit, { netPaid: 4000 })] };
    return {
      visit,
      row: insideRow(reza.fullName, charged, { cafeOrders: [visitCafeOrder, secondCafeOrder] }),
    };
  }

  async function openCancelBox() {
    fireEvent.click(await screen.findByRole("button", { name: "لغو ورود" }));
    return screen.findByRole("dialog");
  }

  /** The first question's button, once the visit's purchases have loaded. */
  async function confirmCancelButton(dialog: HTMLElement) {
    const confirm = within(dialog).getByRole("button", { name: "بله، ورود لغو شود" });
    await waitFor(() => expect(confirm).toBeEnabled());
    return confirm;
  }

  it("Board_CancelWithPurchases_ListsEachWithItsOwnUntickedBox", async () => {
    const { row } = visitWithPurchases();
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () => currentlyInsidePage([row]),
    });

    renderApp("/attendance", { session: session() });
    const dialog = await openCancelBox();

    const purchases = await within(dialog).findByRole("region", { name: "خریدهای این مراجعه" });
    const boxes = within(purchases).getAllByRole("checkbox");
    expect(boxes).toHaveLength(3);
    boxes.forEach((box) => expect(box).not.toBeChecked());
    expect(within(purchases).getByLabelText(/هوازی/)).toBeInTheDocument();
    expect(within(purchases).getByLabelText(/شیک پروتئین × ۱/)).toBeInTheDocument();
    expect(within(purchases).getByLabelText(/آب معدنی × ۱/)).toBeInTheDocument();
    expect(purchases).toHaveTextContent("پرداخت‌شده ۴٬۰۰۰ تومان");
  });

  it("Board_CancelWithNothingTicked_KeepsThePurchasesWithoutASecondQuestion", async () => {
    const { visit, row } = visitWithPurchases();
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () => currentlyInsidePage([row]),
      [`POST /api/attendance/${visit.id}/cancel`]: () =>
        json(200, {
          ...visit,
          checkedOutAt: "2026-09-18T07:05:00Z",
          cancelledAt: "2026-09-18T07:05:00Z",
        }),
    });

    renderApp("/attendance", { session: session() });
    const dialog = await openCancelBox();
    await within(dialog).findByRole("region", { name: "خریدهای این مراجعه" });
    fireEvent.click(await confirmCancelButton(dialog));

    expect(await within(dialog).findByText("ورود لغو شد")).toBeInTheDocument();
    const request = api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`).at(0)!;
    expect(await request.json()).toEqual({ voidCardio: false, cafeOrderIds: [] });
  });

  it("Board_CancelWithTicks_AsksAgainAndSendsOnlyTheTickedOnes", async () => {
    const { visit, row } = visitWithPurchases();
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () => currentlyInsidePage([row]),
      [`POST /api/attendance/${visit.id}/cancel`]: () =>
        json(200, {
          ...visit,
          checkedOutAt: "2026-09-18T07:05:00Z",
          cancelledAt: "2026-09-18T07:05:00Z",
        }),
    });

    renderApp("/attendance", { session: session() });
    const dialog = await openCancelBox();
    fireEvent.click(await within(dialog).findByLabelText(/هوازی/));
    fireEvent.click(within(dialog).getByLabelText(/آب معدنی × ۱/));
    fireEvent.click(await confirmCancelButton(dialog));

    // The second question: what goes, and the reminder about money already collected.
    const cancelling = await within(dialog).findByRole("list", { name: "خریدهای لغوشونده" });
    expect(within(cancelling).getAllByRole("listitem")).toHaveLength(2);
    expect(cancelling).not.toHaveTextContent("شیک پروتئین");
    expect(dialog).toHaveTextContent(
      "اگر وجه این موارد را دریافت کرده‌اید، آن را به عضو بازگردانید؛ اگر دریافت نشده، اقدامی لازم نیست.",
    );
    // 4,000 on the هوازی and 25,000 on the water.
    expect(dialog).toHaveTextContent("طبق ثبت سیستم ۲۹٬۰۰۰ تومان دریافت شده است");
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`)).toHaveLength(0);

    fireEvent.click(within(dialog).getByRole("button", { name: "بله، ورود و این موارد لغو شوند" }));

    expect(await within(dialog).findByText("ورود لغو شد")).toBeInTheDocument();
    expect(dialog).toHaveTextContent("خریدهای انتخاب‌شده هم لغو شد.");
    const request = api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`).at(0)!;
    expect(await request.json()).toEqual({ voidCardio: true, cafeOrderIds: [secondCafeOrder.id] });
  });

  it("Board_CancelSecondQuestion_BackKeepsTheTicksAndSendsNothing", async () => {
    const { visit, row } = visitWithPurchases();
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () => currentlyInsidePage([row]),
    });

    renderApp("/attendance", { session: session() });
    const dialog = await openCancelBox();
    fireEvent.click(await within(dialog).findByLabelText(/شیک پروتئین × ۱/));
    fireEvent.click(await confirmCancelButton(dialog));
    // Nothing was paid on the shake, so there is no figure to hand back.
    expect(
      await within(dialog).findByText(/اگر وجه این موارد را دریافت کرده‌اید/),
    ).toBeInTheDocument();
    expect(dialog).not.toHaveTextContent("طبق ثبت سیستم");

    fireEvent.click(within(dialog).getByRole("button", { name: "بازگشت" }));

    expect(await within(dialog).findByLabelText(/شیک پروتئین × ۱/)).toBeChecked();
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`)).toHaveLength(0);
  });

  it("Board_CancelRefusedForAChangedOrder_ShowsThePersianReason", async () => {
    const { visit, row } = visitWithPurchases();
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () => currentlyInsidePage([row]),
      [`POST /api/attendance/${visit.id}/cancel`]: () =>
        problem(422, "Attendance.CafeOrderNotOnVisit"),
    });

    renderApp("/attendance", { session: session() });
    const dialog = await openCancelBox();
    fireEvent.click(await within(dialog).findByLabelText(/آب معدنی × ۱/));
    fireEvent.click(await confirmCancelButton(dialog));
    fireEvent.click(
      await within(dialog).findByRole("button", { name: "بله، ورود و این موارد لغو شوند" }),
    );

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "یکی از خریدهای بوفه در این فاصله تغییر کرده است.",
    );
  });

  it("Board_CheckOutFails_ShowsThePersianReason", async () => {
    const visit = openVisit(reza.id);
    mockApi({
      ...signedInHandlers(staffUser),
      ...boxHandlers,
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
      [`POST /api/attendance/${visit.id}/check-out`]: () => problem(422, "Attendance.NotOpen"),
    });

    renderApp("/attendance", { session: session() });
    const dialog = await confirmCheckOut();

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "این ورود قبلاً بسته شده است.",
    );
  });

  // ---- A guest (BUSINESS_RULES.md §7 *Guest visit*) ----

  const maryamDrink = {
    ...orderOnAccount,
    id: "0199a000-0000-7000-8000-0000000009f2",
    memberId: null,
    memberFullName: null,
    guestName: "مریم احمدی",
    attendanceId: guestVisit().id,
    totalAmount: 25000,
    netPaid: 0,
    outstanding: 25000,
    paymentStatus: "Unpaid" as const,
    items: [{ ...orderOnAccount.items[0]!, productName: water.name, quantity: 1 }],
  };

  it("Board_GuestInside_ShowsTheNameUnlinkedAndGuestWhereTheSessionsGo", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([guestInsideRow(guestVisit("مریم احمدی"))]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByText("مریم احمدی")).closest("tr")!;
    expect(within(row).queryByRole("link")).not.toBeInTheDocument();
    expect(row).toHaveTextContent("مهمان");
    expect(within(row).queryByRole("progressbar")).not.toBeInTheDocument();
    // Never marked as needing attention: a guest has no plan to run out.
    expect(row.querySelector(".text-warning")).toBeNull();
  });

  it("Board_CancelGuestLeavingAnUnpaidOrder_WaitsUntilItIsTickedAndSendsNoCardio", async () => {
    const visit = guestVisit("مریم احمدی");
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([guestInsideRow(visit, [maryamDrink])]),
      [`POST /api/attendance/${visit.id}/cancel`]: () =>
        json(200, { ...visit, checkedOutAt: visit.checkedInAt, cancelledAt: visit.checkedInAt }),
    });

    renderApp("/attendance", { session: session() });
    const dialog = await openCancelBox();

    // No session to give back, and an order left standing would be a debt with nobody to owe it.
    await within(dialog).findByRole("region", { name: "خریدهای این مراجعه" });
    expect(dialog).not.toHaveTextContent("جلسه به اشتراک او بازمی‌گردد");
    const confirm = within(dialog).getByRole("button", { name: "بله، ورود لغو شود" });
    expect(confirm).toBeDisabled();
    expect(dialog).toHaveTextContent("سفارش پرداخت‌نشده‌ی مهمان را تیک بزنید");

    fireEvent.click(within(dialog).getByLabelText(/آب معدنی × ۱/));
    await waitFor(() => expect(confirm).toBeEnabled());
    fireEvent.click(confirm);
    expect(dialog).toHaveTextContent("آن را به مهمان بازگردانید");
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، ورود و این موارد لغو شوند" }));

    expect(await within(dialog).findByText("ورود لغو شد")).toBeInTheDocument();
    expect(dialog).not.toHaveTextContent("جلسه به اشتراک بازگشت");
    const request = api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`).at(0)!;
    expect(await request.json()).toEqual({ voidCardio: false, cafeOrderIds: [maryamDrink.id] });
  });

  it("Board_CheckOutGuestWithUnpaidCafe_ShowsWhyFromTheApi", async () => {
    const visit = guestVisit("مریم احمدی");
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([guestInsideRow(visit, [maryamDrink])]),
      "GET /api/cafe/orders": () =>
        json(200, { items: [maryamDrink], page: 1, pageSize: 100, totalCount: 1 }),
      [`POST /api/attendance/${visit.id}/check-out`]: () =>
        problem(422, "Attendance.GuestHasUnpaidCafe"),
    });

    renderApp("/attendance", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: "ثبت خروج" }));
    const dialog = await screen.findByRole("dialog");
    // What the guest bought is listed; there is no plan or account debt to show.
    expect(await within(dialog).findByRole("region", { name: "خریدهای بوفه" })).toBeInTheDocument();
    fireEvent.click(within(dialog).getByLabelText("کلید کمد شماره ۳ را تحویل گرفتم"));
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("«تسویه یکجا»");
  });
});
