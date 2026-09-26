import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { cardioCharge, currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { memberDebt, reza } from "@/test/members";
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

  /** A bar needs a denominator. An unlimited subscription has none, so it says so instead. */
  it("Board_UnlimitedSubscription_SaysUnlimitedAndDrawsNoBar", async () => {
    const visit = openVisit(reza.id);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([
          insideRow(reza.fullName, visit, {
            totalSessions: null,
            usedSessions: 9,
            remainingSessions: null,
          }),
        ]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(within(row).getByText("نامحدود")).toBeInTheDocument();
    expect(within(row).queryByRole("progressbar")).not.toBeInTheDocument();
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
   * BUSINESS_RULES.md §7 Gym services: the treadmill amount is typed while the member is inside,
   * so the board takes it without anybody opening a profile (roadmap 5.7).
   */
  it("Board_SomeoneInside_TakesACardioAmountFromTheBoard", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
      [`POST /api/attendance/${visit.id}/service-charges`]: () =>
        json(201, cardioCharge(visit, { amount: 10000 })),
    });

    renderApp("/attendance", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "مبلغ هوازی" }));
    fireEvent.change(screen.getByLabelText("مبلغ هوازی"), { target: { value: "10000" } });
    fireEvent.click(screen.getByRole("button", { name: "ثبت" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/attendance/${visit.id}/service-charges`)).toHaveLength(1),
    );
    // The desk is told it went through, rather than watching the dialog vanish.
    expect(await screen.findByText("مبلغ هوازی ثبت شد")).toBeInTheDocument();
  });

  it("Board_ChargedVisit_ShowsTheAmountOnTheRow", async () => {
    const visit = openVisit(reza.id);
    const charged = { ...visit, serviceCharges: [cardioCharge(visit)] };
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, charged)]),
    });

    renderApp("/attendance", { session: session() });

    const row = (await screen.findByRole("link", { name: reza.fullName })).closest("tr")!;
    expect(row).toHaveTextContent("۱۰٬۰۰۰ تومان");
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
    fireEvent.click(await screen.findByRole("button", { name: "لغو ورود" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، ورود لغو شود" }));

    expect(await within(dialog).findByText("ورود لغو شد")).toBeInTheDocument();
    expect(dialog).toHaveTextContent("جلسه به اشتراک بازگشت");
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`)).toHaveLength(1);
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
});
