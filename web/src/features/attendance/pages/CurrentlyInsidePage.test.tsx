import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { cardioCharge, currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

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
    const inThreeDays = new Date(Date.now() + 3 * 86_400_000).toISOString().slice(0, 10);
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

  it("Board_SubscriptionNotExpiringSoon_ShowsNoDaysLeft", async () => {
    const visit = openVisit(reza.id);
    const inTwoMonths = new Date(Date.now() + 60 * 86_400_000).toISOString().slice(0, 10);
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

    fireEvent.click(await screen.findByRole("button", { name: "افزودن مبلغ هوازی" }));
    fireEvent.change(screen.getByLabelText("مبلغ هوازی"), { target: { value: "10000" } });
    fireEvent.click(screen.getByRole("button", { name: "ثبت" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/attendance/${visit.id}/service-charges`)).toHaveLength(1),
    );
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

  it("Board_CheckOut_CallsTheApiAndRefreshesTheBoard", async () => {
    const visit = openVisit(reza.id);
    let stillInside = true;
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage(stillInside ? [insideRow(reza.fullName, visit)] : []),
      [`POST /api/attendance/${visit.id}/check-out`]: () => {
        stillInside = false;
        return json(200, { ...visit, checkedOutAt: "2026-09-18T09:00:00Z" });
      },
    });

    renderApp("/attendance", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ثبت خروج" }));

    expect(await screen.findByText("در حال حاضر کسی داخل باشگاه نیست.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/check-out`)).toHaveLength(1);
  });

  it("Board_CancelCheckIn_ShowsTheConfirmation", async () => {
    const visit = openVisit(reza.id);
    mockApi({
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

    expect(await screen.findByRole("status")).toHaveTextContent("جلسه به اشتراک بازگشت");
  });

  it("Board_CheckOutFails_ShowsThePersianReason", async () => {
    const visit = openVisit(reza.id);
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
      [`POST /api/attendance/${visit.id}/check-out`]: () => problem(422, "Attendance.NotOpen"),
    });

    renderApp("/attendance", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ثبت خروج" }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent("این ورود قبلاً بسته شده است."),
    );
  });
});
