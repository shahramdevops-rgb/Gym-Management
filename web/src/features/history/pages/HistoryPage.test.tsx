import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { planLabel } from "@/features/subscriptions/planLabel";
import { formatMoney, gymToday } from "@/lib/format";
import {
  autoClosedVisit,
  cafeSale,
  cancelledSingleVisitSale,
  cancelledVisit,
  closedVisit,
  guestCafePayment,
  guestVisit,
  historyPage,
  liveCardio,
  planPayment,
  planRefund,
  planSale,
  settledCafePayment,
  settledVisitPayment,
  shopSale,
  voidedCardio,
  walkInCafePayment,
} from "@/test/history";
import { reza, membersPage } from "@/test/members";
import { json, mockApi, owner, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";
import { dateShown } from "@/test/jalaliDate";

import { isoDaysBefore } from "../range";

function handlers(user = owner) {
  return {
    ...signedInHandlers(user),
    "GET /api/attendance": () => historyPage([closedVisit, autoClosedVisit, cancelledVisit]),
    "GET /api/payments": () => historyPage([planRefund, planPayment, walkInCafePayment]),
    "GET /api/service-charges": () => historyPage([liveCardio, voidedCardio]),
    "GET /api/sales": () => historyPage([cafeSale, shopSale, planSale, cancelledSingleVisitSale]),
    "GET /api/sales/totals": () =>
      json(200, { amount: 2160000, netPaid: 1260000, remaining: 900000 }),
    "GET /api/payments/totals": () => json(200, { received: 960000, refunded: 900000, net: 60000 }),
    [`GET /api/members/${reza.id}`]: () => json(200, reza),
    "GET /api/members": () => membersPage([reza]),
  };
}

function queryOf(request: Request | undefined): URLSearchParams {
  if (request === undefined) {
    throw new Error("The request was never made.");
  }
  return new URL(request.url).searchParams;
}

function rowWith(text: string): HTMLElement {
  const row = screen.getAllByText(text)[0]!.closest("tr");
  if (row === null) {
    throw new Error(`No row shows ${text}`);
  }
  return row;
}

describe("HistoryPage", () => {
  it("Attendance_Loaded_ShowsWhoCheckedInAndMarksCancelledAndAutomatic", async () => {
    mockApi(handlers(staffUser));
    renderApp("/history", { session: session() });

    expect(await screen.findByRole("heading", { name: "تاریخچه" })).toBeInTheDocument();
    expect((await screen.findAllByRole("link", { name: reza.fullName }))[0]).toHaveAttribute(
      "href",
      `/members/${reza.id}`,
    );

    const automatic = rowWith("خودکار");
    expect(within(automatic).getByText("رزرو")).toBeInTheDocument();
    expect(within(automatic).getByText("مدیر باشگاه")).toBeInTheDocument();

    expect(within(rowWith("لغو شده")).getByText("سارا رضایی")).toBeInTheDocument();
    expect(within(rowWith("۱۲")).getByText("خارج شده")).toBeInTheDocument();
  });

  it("Attendance_GuestVisit_ShowsTheNameMarkedAsGuestWithNoProfileLink", async () => {
    mockApi({
      ...handlers(),
      "GET /api/attendance": () => historyPage([guestVisit]),
    });
    renderApp("/history", { session: session() });

    await screen.findByText("مریم احمدی");
    const row = rowWith("مریم احمدی");
    expect(within(row).getByText("مهمان")).toBeInTheDocument();
    expect(within(row).queryByRole("link")).not.toBeInTheDocument();
  });

  it("Payments_GuestsCafeOrder_ShowsTheGuestsNameNotAWalkIn", async () => {
    mockApi({
      ...handlers(),
      "GET /api/payments": () => historyPage([guestCafePayment]),
    });
    renderApp("/history?tab=payments", { session: session() });

    await screen.findByText("مریم احمدی");
    const row = rowWith("مریم احمدی");
    expect(within(row).getByText("مهمان")).toBeInTheDocument();
    expect(within(row).queryByText("مشتری آزاد")).not.toBeInTheDocument();
  });

  it("Attendance_NoDatesInTheUrl_AsksForToday", async () => {
    const api = mockApi(handlers());
    renderApp("/history", { session: session() });

    await screen.findByText("خودکار");
    const query = queryOf(api.requestsTo("GET", "/api/attendance")[0]);
    expect(query.get("From")).toBe(gymToday());
    expect(query.get("To")).toBe(gymToday());
  });

  it("Attendance_ClearedDatesInTheUrl_AskWithNoBound", async () => {
    const api = mockApi(handlers());
    renderApp("/history?from=&to=", { session: session() });

    await screen.findByText("خودکار");
    const query = queryOf(api.requestsTo("GET", "/api/attendance")[0]);
    expect(query.has("From")).toBe(false);
    expect(query.has("To")).toBe(false);
  });

  it("Attendance_BackwardsRange_SaysSoWithoutAskingTheApi", async () => {
    const api = mockApi(handlers());
    renderApp("/history?from=2026-09-30&to=2026-09-01", { session: session() });

    expect(await screen.findByText("بازهٔ تاریخ نامعتبر است.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/attendance")).toHaveLength(0);
  });

  it("Payments_Loaded_MarksRefundsUndoneItemsAndWalkIns", async () => {
    mockApi(handlers());
    renderApp("/history?tab=payments", { session: session() });

    await screen.findByText("انصراف عضو");
    const refund = rowWith("انصراف عضو");
    expect(within(refund).getByText("استرداد")).toBeInTheDocument();
    expect(within(refund).getByText("لغو شده")).toBeInTheDocument();
    expect(within(refund).getByText("مدیر باشگاه")).toBeInTheDocument();
    expect(within(refund).getByText("۱۲ جلسه - ۳۰ روزه")).toBeInTheDocument();

    const walkIn = rowWith("مشتری آزاد");
    expect(within(walkIn).getByText("بوفه")).toBeInTheDocument();
    expect(within(walkIn).queryByText("لغو شده")).not.toBeInTheDocument();
    expect(within(walkIn).getByText("نقدی")).toBeInTheDocument();
  });

  it("Payments_OneSettlement_ShowsOneHeadingWithTheTotalAndItsItemsUnderIt", async () => {
    mockApi({
      ...handlers(),
      "GET /api/payments": () =>
        historyPage([settledVisitPayment, settledCafePayment, walkInCafePayment]),
    });
    renderApp("/history?tab=payments", { session: session() });

    await screen.findByText("تسویه یکجا");
    const heading = rowWith("تسویه یکجا");
    expect(within(heading).getByText("(۲ قلم)")).toBeInTheDocument();
    expect(within(heading).getByText(formatMoney(240000))).toBeInTheDocument();
    expect(within(heading).getByText("انتقال بانکی")).toBeInTheDocument();
    expect(within(heading).getByText(reza.fullName)).toBeInTheDocument();

    // What the rows share is said once, on the heading (the method filter lists it too).
    const table = screen.getByRole("table");
    expect(within(table).getAllByText(reza.fullName)).toHaveLength(1);
    expect(within(table).getAllByText("انتقال بانکی")).toHaveLength(1);

    const visit = rowWith(planLabel({ durationDays: 1, totalSessions: 1, isSingleSession: true }));
    expect(within(visit).getByText(formatMoney(180000))).toBeInTheDocument();
    const cafe = rowWith(formatMoney(60000));
    expect(within(cafe).getByText("بوفه")).toBeInTheDocument();
  });

  it("Payments_FiltersInTheUrl_AreSentAndShown", async () => {
    const api = mockApi(handlers());
    renderApp(
      `/history?tab=payments&from=2026-09-01&to=2026-09-30&member=${reza.id}&method=Cash&source=CafeOrder`,
      { session: session() },
    );

    await screen.findByText("انصراف عضو");
    const query = queryOf(api.requestsTo("GET", "/api/payments")[0]);
    expect(query.get("From")).toBe("2026-09-01");
    expect(query.get("To")).toBe("2026-09-30");
    expect(query.get("MemberId")).toBe(reza.id);
    expect(query.get("Method")).toBe("Cash");
    expect(query.get("Source")).toBe("CafeOrder");

    expect(dateShown("از تاریخ")).toBe("۱۰ شهریور ۱۴۰۵");
    expect(screen.getByLabelText("روش پرداخت")).toHaveValue("Cash");
    expect(screen.getByLabelText("بابت")).toHaveValue("CafeOrder");
    expect(await screen.findByText(reza.fullName, { selector: "span" })).toBeInTheDocument();
  });

  /** آنالیز is its own «بابت», sent to the API as a service charge of that kind (task 6.5.29). */
  it("Payments_ChoosingAnalysis_AsksForServiceChargesOfThatKind", async () => {
    const api = mockApi(handlers());
    renderApp("/history?tab=payments", { session: session() });

    await screen.findByText("انصراف عضو");
    fireEvent.change(screen.getByLabelText("بابت"), { target: { value: "Analysis" } });

    await waitFor(() => {
      const query = queryOf(api.requestsTo("GET", "/api/payments").at(-1));
      expect(query.get("Source")).toBe("ServiceCharge");
      expect(query.get("ServiceKind")).toBe("Analysis");
    });
    expect(screen.getByLabelText("بابت")).toHaveDisplayValue("آنالیز");
  });

  it("Payments_ChoosingAMethod_PutsItInTheUrlAndAsksAgain", async () => {
    const api = mockApi(handlers());
    const { router } = renderApp("/history?tab=payments", { session: session() });

    await screen.findByText("انصراف عضو");
    fireEvent.change(screen.getByLabelText("روش پرداخت"), { target: { value: "Card" } });

    await waitFor(() => expect(router.state.location.search).toBe("?tab=payments&method=Card"));
    await waitFor(() =>
      expect(queryOf(api.requestsTo("GET", "/api/payments").at(-1)).get("Method")).toBe("Card"),
    );
  });

  it("Payments_StaffAskingForFourDaysAgo_SaysSoWithoutAskingTheApi", async () => {
    const api = mockApi(handlers(staffUser));
    renderApp(`/history?tab=payments&from=${isoDaysBefore(gymToday(), 4)}`, {
      session: session(),
    });

    expect(
      await screen.findByText("کارمندان فقط پرداخت‌های امروز و ۳ روز قبل از آن را می‌بینند."),
    ).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/payments")).toHaveLength(0);
  });

  it("Payments_StaffWithTheStartCleared_SaysSoWithoutAskingTheApi", async () => {
    const api = mockApi(handlers(staffUser));
    renderApp("/history?tab=payments&from=", { session: session() });

    expect(
      await screen.findByText("کارمندان فقط پرداخت‌های امروز و ۳ روز قبل از آن را می‌بینند."),
    ).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/payments")).toHaveLength(0);
  });

  it("Payments_StaffAskingForThreeDaysAgo_AsksTheApi", async () => {
    const api = mockApi(handlers(staffUser));
    const threeDaysAgo = isoDaysBefore(gymToday(), 3);
    renderApp(`/history?tab=payments&from=${threeDaysAgo}`, { session: session() });

    await screen.findByText("انصراف عضو");
    expect(queryOf(api.requestsTo("GET", "/api/payments")[0]).get("From")).toBe(threeDaysAgo);
    expect(screen.getByText(/پرداخت‌های امروز و ۳ روز قبل از آن را می‌بینید/)).toBeInTheDocument();
  });

  it("Payments_OwnerAskingForLastYear_AsksTheApi", async () => {
    const api = mockApi(handlers());
    renderApp("/history?tab=payments&from=2025-10-01", { session: session() });

    await screen.findByText("انصراف عضو");
    expect(queryOf(api.requestsTo("GET", "/api/payments")[0]).get("From")).toBe("2025-10-01");
    expect(screen.queryByText(/۳ روز قبل/)).not.toBeInTheDocument();
  });

  it("Cardio_Loaded_ShowsVoidedChargeWithItsReasonAndWhoVoidedIt", async () => {
    mockApi(handlers(staffUser));
    renderApp("/history?tab=cardio", { session: session() });

    await screen.findByText("ابطال شده");
    const voided = rowWith("ابطال شده");
    expect(within(voided).getByText(/صفر اضافه — مدیر باشگاه/)).toBeInTheDocument();
    expect(within(voided).getByText("۵۰۰٬۰۰۰ تومان")).toBeInTheDocument();

    const live = rowWith("۵۰٬۰۰۰ تومان");
    expect(within(live).getByText("پرداخت‌نشده")).toBeInTheDocument();
    expect(within(live).getByText("سارا رضایی")).toBeInTheDocument();
  });

  it("Cardio_GuestsCharge_ShowsTheGuestsNameMarkedAsGuestWithNoProfileLink", async () => {
    const guestCardio = {
      ...liveCardio,
      memberId: null,
      memberFullName: null,
      guestName: "مریم احمدی",
    };
    mockApi({
      ...handlers(staffUser),
      "GET /api/service-charges": () => historyPage([guestCardio]),
    });
    renderApp("/history?tab=cardio", { session: session() });

    const row = (await screen.findByText("مریم احمدی")).closest("tr")!;
    expect(row).toHaveTextContent("مهمان");
    expect(within(row).queryByRole("link")).not.toBeInTheDocument();
  });

  it("Tabs_Switching_KeepsTheRangeAndTheMember", async () => {
    // Staff: the combined section is theirs; the Owner has the sales sections instead.
    const api = mockApi(handlers(staffUser));
    const { router } = renderApp(`/history?from=2026-09-01&member=${reza.id}`, {
      session: session(),
    });

    await screen.findByText("خودکار");
    fireEvent.click(screen.getByRole("tab", { name: "هوازی، فروشگاه و آنالیز" }));

    await waitFor(() =>
      expect(router.state.location.search).toBe(`?tab=cardio&from=2026-09-01&member=${reza.id}`),
    );
    await screen.findByText("ابطال شده");
    const query = queryOf(api.requestsTo("GET", "/api/service-charges")[0]);
    expect(query.get("From")).toBe("2026-09-01");
    expect(query.get("To")).toBe(gymToday());
    expect(query.get("MemberId")).toBe(reza.id);
  });

  /** BUSINESS_RULES.md §12 Sales in the history (task 6.5.30). */
  it("Sales_OwnerTabs_ListEachKindAndReplaceTheCombinedSection", async () => {
    mockApi(handlers());
    renderApp("/history", { session: session() });

    const tablist = await screen.findByRole("tablist", { name: "بخش‌های تاریخچه" });
    await waitFor(() =>
      expect(
        within(tablist)
          .getAllByRole("tab")
          .map((tab) => tab.textContent),
      ).toEqual([
        "ورود و خروج",
        "پرداخت‌ها",
        "همهٔ فروش‌ها",
        "فروش پلن",
        "هوازی",
        "فروشگاه",
        "آنالیز",
        "بوفه",
      ]),
    );
  });

  it("Sales_Staff_KeepTheirThreeSectionsAndNeverAskForSales", async () => {
    const api = mockApi(handlers(staffUser));
    renderApp("/history?tab=sales", { session: session() });

    await screen.findByText("خودکار");
    const tablist = screen.getByRole("tablist", { name: "بخش‌های تاریخچه" });
    expect(
      within(tablist)
        .getAllByRole("tab")
        .map((tab) => tab.textContent),
    ).toEqual(["ورود و خروج", "پرداخت‌ها", "هوازی، فروشگاه و آنالیز"]);
    expect(screen.getByRole("tab", { name: "ورود و خروج" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(api.requestsTo("GET", "/api/sales")).toHaveLength(0);
  });

  it("Sales_AllSales_ShowsEveryKindWithWhatItSoldAndMarksTheCancelled", async () => {
    const api = mockApi(handlers());
    renderApp("/history?tab=sales", { session: session() });

    await screen.findByText("پلن ۱۲ جلسه - ۳۰ روزه");
    expect(queryOf(api.requestsTo("GET", "/api/sales")[0]).has("Source")).toBe(false);
    expect(queryOf(api.requestsTo("GET", "/api/sales")[0]).has("Paid")).toBe(false);

    const plan = rowWith("پلن ۱۲ جلسه - ۳۰ روزه");
    expect(within(plan).getByText("پرداخت جزئی")).toBeInTheDocument();
    expect(within(plan).getByText(formatMoney(1200000))).toBeInTheDocument();

    expect(within(rowWith("فروشگاه: دستکش × ۲")).getByText("سارا رضایی")).toBeInTheDocument();

    const cafe = rowWith("بوفه: آب معدنی × ۲، کیک");
    expect(within(cafe).getByText("مشتری آزاد")).toBeInTheDocument();
    expect(within(cafe).getByText("پرداخت‌شده")).toBeInTheDocument();

    const cancelled = rowWith("پلن تک‌جلسه‌ای");
    expect(within(cancelled).getByText("لغو شده")).toBeInTheDocument();
    expect(within(cancelled).getByText("اشتباه در ثبت")).toBeInTheDocument();
    expect(within(cancelled).queryByText("پرداخت‌نشده")).not.toBeInTheDocument();
  });

  it("Sales_EachTab_AsksForItsOwnKind", async () => {
    const api = mockApi(handlers());
    renderApp("/history?tab=sales", { session: session() });
    await screen.findByText("پلن ۱۲ جلسه - ۳۰ روزه");

    const expected: [string, string][] = [
      ["فروش پلن", "Subscription"],
      ["هوازی", "Cardio"],
      ["فروشگاه", "Miscellaneous"],
      ["آنالیز", "Analysis"],
      ["بوفه", "CafeOrder"],
    ];
    for (const [label, source] of expected) {
      fireEvent.click(screen.getByRole("tab", { name: label }));
      await waitFor(() =>
        expect(queryOf(api.requestsTo("GET", "/api/sales").at(-1)).get("Source")).toBe(source),
      );
    }
  });

  it("Sales_PaidButton_PutsItInTheUrlAndAsksAgain", async () => {
    const api = mockApi(handlers());
    const { router } = renderApp("/history?tab=sales-plans", { session: session() });
    await screen.findByText("پلن ۱۲ جلسه - ۳۰ روزه");

    const group = screen.getByRole("group", { name: "وضعیت پرداخت" });
    expect(within(group).getByRole("button", { name: "همه" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    fireEvent.click(within(group).getByRole("button", { name: "پرداخت نشده" }));

    await waitFor(() => expect(router.state.location.search).toBe("?tab=sales-plans&paid=Unpaid"));
    await waitFor(() => {
      const query = queryOf(api.requestsTo("GET", "/api/sales").at(-1));
      expect(query.get("Paid")).toBe("Unpaid");
      expect(query.get("Source")).toBe("Subscription");
    });
    expect(within(group).getByRole("button", { name: "پرداخت نشده" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );

    fireEvent.click(within(group).getByRole("button", { name: "همه" }));
    await waitFor(() => expect(router.state.location.search).toBe("?tab=sales-plans"));
  });

  it("Totals_SalesSection_OwnerSeesAmountPaidAndRemainingForTheSameFilters", async () => {
    const api = mockApi(handlers());
    renderApp("/history?tab=sales-cardio&paid=Unpaid", { session: session() });

    const totals = await screen.findByRole("region", { name: "جمع" });
    expect(within(totals).getByText("جمع همهٔ ردیف‌ها")).toBeInTheDocument();
    expect(within(totals).getByText("مبلغ").nextSibling).toHaveTextContent(formatMoney(2160000));
    expect(within(totals).getByText("دریافتی").nextSibling).toHaveTextContent(formatMoney(1260000));
    expect(within(totals).getByText("مانده").nextSibling).toHaveTextContent(formatMoney(900000));

    // The list's own filters, and no page: the totals cover every page.
    const query = queryOf(api.requestsTo("GET", "/api/sales/totals")[0]);
    expect(query.get("Source")).toBe("Cardio");
    expect(query.get("Paid")).toBe("Unpaid");
    expect(query.get("From")).toBe(gymToday());
    expect(query.get("To")).toBe(gymToday());
    expect(query.has("Page")).toBe(false);
  });

  it("Totals_PaymentsSection_OwnerSeesReceivedRefundedAndNet", async () => {
    const api = mockApi(handlers());
    renderApp("/history?tab=payments&method=Cash&source=Cardio", { session: session() });

    const totals = await screen.findByRole("region", { name: "جمع" });
    expect(within(totals).getByText("دریافتی").nextSibling).toHaveTextContent(formatMoney(960000));
    expect(within(totals).getByText("بازگشت").nextSibling).toHaveTextContent(formatMoney(900000));
    expect(within(totals).getByText("خالص").nextSibling).toHaveTextContent(formatMoney(60000));

    const query = queryOf(api.requestsTo("GET", "/api/payments/totals")[0]);
    expect(query.get("Method")).toBe("Cash");
    expect(query.get("Source")).toBe("ServiceCharge");
    expect(query.get("ServiceKind")).toBe("Cardio");
    expect(query.has("Page")).toBe(false);
  });

  it("Totals_Staff_NeverAskAndSeeNone", async () => {
    const api = mockApi(handlers(staffUser));
    renderApp("/history?tab=payments", { session: session() });

    await screen.findAllByText(formatMoney(900000));
    expect(screen.queryByRole("region", { name: "جمع" })).not.toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/payments/totals")).toHaveLength(0);
    expect(api.requestsTo("GET", "/api/sales/totals")).toHaveLength(0);
  });

  it("Totals_NoRows_ShowsNone", async () => {
    mockApi({ ...handlers(), "GET /api/sales": () => historyPage([]) });
    renderApp("/history?tab=sales", { session: session() });

    expect(await screen.findByText("در این بازه چیزی ثبت نشده است.")).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "جمع" })).not.toBeInTheDocument();
  });

  it("MemberFilter_PickingAMember_PutsItInTheUrlAndAsksForTheirRows", async () => {
    const api = mockApi(handlers());
    const { router } = renderApp("/history", { session: session() });

    await screen.findByText("خودکار");
    fireEvent.change(screen.getByLabelText("عضو"), { target: { value: "رضا" } });
    fireEvent.click(await screen.findByRole("button", { name: /رضا احمدی/ }));

    await waitFor(() => expect(router.state.location.search).toBe(`?member=${reza.id}`));
    await waitFor(() =>
      expect(queryOf(api.requestsTo("GET", "/api/attendance").at(-1)).get("MemberId")).toBe(
        reza.id,
      ),
    );

    fireEvent.click(await screen.findByRole("button", { name: "همهٔ اعضا" }));
    await waitFor(() => expect(router.state.location.search).toBe(""));
  });

  it("Navigation_BothRoles_OfferTheHistory", async () => {
    mockApi(handlers(staffUser));
    renderApp("/history", { session: session() });

    const navigation = await screen.findByRole("navigation", { name: "منوی اصلی" });
    expect(within(navigation).getByRole("link", { name: "تاریخچه" })).toHaveAttribute(
      "href",
      "/history",
    );
    // The cafe keeps its own order history, one click away.
    expect(
      within(screen.getByRole("main")).getByRole("link", { name: "سفارش‌های بوفه" }),
    ).toHaveAttribute("href", "/cafe/orders");
  });
});
