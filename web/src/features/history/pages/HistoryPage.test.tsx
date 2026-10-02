import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { planLabel } from "@/features/subscriptions/planLabel";
import { formatMoney, gymToday } from "@/lib/format";
import {
  autoClosedVisit,
  cancelledVisit,
  closedVisit,
  guestCafePayment,
  guestVisit,
  historyPage,
  liveCardio,
  planPayment,
  planRefund,
  settledCafePayment,
  settledVisitPayment,
  voidedCardio,
  walkInCafePayment,
} from "@/test/history";
import { reza, membersPage } from "@/test/members";
import { json, mockApi, owner, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

import { isoDaysBefore } from "../range";

function handlers(user = owner) {
  return {
    ...signedInHandlers(user),
    "GET /api/attendance": () => historyPage([closedVisit, autoClosedVisit, cancelledVisit]),
    "GET /api/payments": () => historyPage([planRefund, planPayment, walkInCafePayment]),
    "GET /api/service-charges": () => historyPage([liveCardio, voidedCardio]),
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

    expect(screen.getByLabelText("از تاریخ")).toHaveValue("۱۴۰۵/۰۶/۱۰");
    expect(screen.getByLabelText("روش پرداخت")).toHaveValue("Cash");
    expect(screen.getByLabelText("بابت")).toHaveValue("CafeOrder");
    expect(await screen.findByText(reza.fullName, { selector: "span" })).toBeInTheDocument();
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

  it("Tabs_Switching_KeepsTheRangeAndTheMember", async () => {
    const api = mockApi(handlers());
    const { router } = renderApp(`/history?from=2026-09-01&member=${reza.id}`, {
      session: session(),
    });

    await screen.findByText("خودکار");
    fireEvent.click(screen.getByRole("tab", { name: "هوازی" }));

    await waitFor(() =>
      expect(router.state.location.search).toBe(`?tab=cardio&from=2026-09-01&member=${reza.id}`),
    );
    await screen.findByText("ابطال شده");
    const query = queryOf(api.requestsTo("GET", "/api/service-charges")[0]);
    expect(query.get("From")).toBe("2026-09-01");
    expect(query.get("To")).toBe(gymToday());
    expect(query.get("MemberId")).toBe(reza.id);
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
