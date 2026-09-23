import { fireEvent, screen, waitFor } from "@testing-library/react";

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
