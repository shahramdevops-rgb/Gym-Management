import { fireEvent, screen, waitFor } from "@testing-library/react";

import { attendanceHistoryPage, cardioCharge, closedVisit, openVisit } from "@/test/attendance";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { memberDebt, reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";
import { subscriptionsPage } from "@/test/subscriptions";
import type { Attendance } from "@/features/attendance/api";
import type { ServiceCharge } from "@/features/serviceCharges/api";

/**
 * The هوازی slot on a member's open visit (BUSINESS_RULES.md §7 Gym services). Rendered through
 * the profile page rather than on its own, because what the front desk can do to a charge depends
 * on the visit it hangs off, and the page is what puts the two together.
 */
describe("ServiceChargeBox", () => {
  function renderProfile(visit: Attendance, extra: Record<string, () => Response> = {}) {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt(),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([visit]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      ...extra,
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    return api;
  }

  function withCharge(visit: Attendance, overrides: Partial<ServiceCharge> = {}): Attendance {
    return { ...visit, serviceCharges: [cardioCharge(visit, overrides)] };
  }

  it("Box_OpenVisitWithNoCharge_OffersToAddOne", async () => {
    renderProfile(openVisit(reza.id));

    expect(await screen.findByRole("button", { name: "افزودن مبلغ هوازی" })).toBeInTheDocument();
  });

  /**
   * The amount in words is the guard against a miscounted zero (BUSINESS_RULES.md §13), so the
   * هوازی box has to be the shared MoneyField and not a plain input.
   */
  it("Box_TypingAnAmount_ShowsItInPersianWordsAndPostsIt", async () => {
    const visit = openVisit(reza.id);
    const api = renderProfile(visit, {
      [`POST /api/attendance/${visit.id}/service-charges`]: () =>
        json(201, cardioCharge(visit, { amount: 10000 })),
    });

    fireEvent.click(await screen.findByRole("button", { name: "افزودن مبلغ هوازی" }));
    fireEvent.change(screen.getByLabelText("مبلغ هوازی"), { target: { value: "10000" } });

    expect(screen.getByLabelText("مبلغ هوازی")).toHaveValue("۱۰٬۰۰۰");
    expect(screen.getByText("ده هزار تومان")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "ثبت" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/attendance/${visit.id}/service-charges`)).toHaveLength(1),
    );
  });

  it("Box_ServerRefusesTheAmount_ShowsThePersianMessageOnTheField", async () => {
    const visit = openVisit(reza.id);
    renderProfile(visit, {
      [`POST /api/attendance/${visit.id}/service-charges`]: () =>
        problem(409, "ServiceCharges.AlreadyCharged"),
    });

    fireEvent.click(await screen.findByRole("button", { name: "افزودن مبلغ هوازی" }));
    fireEvent.change(screen.getByLabelText("مبلغ هوازی"), { target: { value: "10000" } });
    fireEvent.click(screen.getByRole("button", { name: "ثبت" }));

    expect(
      await screen.findByText("برای این ورود قبلاً مبلغ هوازی ثبت شده است."),
    ).toBeInTheDocument();
  });

  /**
   * Nothing settled yet, so every action is still open: correct the amount, take the money, or
   * undo it altogether.
   */
  it("Box_UnpaidChargeOnAnOpenVisit_OffersEditPayAndVoid", async () => {
    renderProfile(withCharge(openVisit(reza.id)));

    expect(await screen.findByRole("button", { name: "ویرایش مبلغ" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "ثبت پرداخت" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "ابطال" })).toBeInTheDocument();
    expect(screen.getByText("مانده: ۱۰٬۰۰۰ تومان")).toBeInTheDocument();
  });

  /**
   * BUSINESS_RULES.md §7: once money has been taken it is a financial record, corrected with a
   * void and a reason. The button that would edit it is gone rather than disabled — the same
   * decision as the subscription history row in task 4.7.
   */
  it("Box_PaidCharge_OffersOnlyVoid", async () => {
    renderProfile(
      withCharge(openVisit(reza.id), {
        netPaid: 10000,
        paymentStatus: "Paid",
        canChangeAmount: false,
      }),
    );

    expect(await screen.findByRole("button", { name: "ابطال" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ویرایش مبلغ" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ثبت پرداخت" })).not.toBeInTheDocument();
  });

  /**
   * Debt outlives the thing that created it (BUSINESS_RULES.md §5). The visit is over and has left
   * the card at the top of the page, so the money is taken from its row in the history instead —
   * and the edit, which the closed visit no longer allows, is gone.
   */
  it("Box_UnpaidChargeOnAClosedVisit_StillOffersToTakeThePaymentFromTheHistoryRow", async () => {
    const visit = closedVisit(reza.id);
    renderProfile(withCharge(visit, { canChangeAmount: false }));

    expect(await screen.findByRole("button", { name: "ثبت پرداخت" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ویرایش مبلغ" })).not.toBeInTheDocument();
  });

  /** A settled charge on a finished visit offers nothing but the void that undoes it. */
  it("Box_PaidChargeOnAClosedVisit_OffersOnlyVoid", async () => {
    const visit = closedVisit(reza.id);
    renderProfile(
      withCharge(visit, { netPaid: 10000, paymentStatus: "Paid", canChangeAmount: false }),
    );

    expect(await screen.findByRole("button", { name: "ابطال" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ثبت پرداخت" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ویرایش مبلغ" })).not.toBeInTheDocument();
  });

  it("Box_Voiding_RequiresAReasonAndWarnsThatMoneyComesBack", async () => {
    const visit = openVisit(reza.id);
    const charge = cardioCharge(visit, { netPaid: 10000, paymentStatus: "Paid", canChangeAmount: false });
    const api = renderProfile(
      { ...visit, serviceCharges: [charge] },
      {
        [`POST /api/service-charges/${charge.id}/void`]: () =>
          json(200, { ...charge, voidedAt: "2026-09-18T07:30:00Z", voidReason: "اشتباه بود" }),
      },
    );

    fireEvent.click(await screen.findByRole("button", { name: "ابطال" }));

    expect(
      screen.getByText("مبلغی که برای این مورد پرداخت شده است، با همین دلیل به عضو بازگردانده می‌شود."),
    ).toBeInTheDocument();

    // A blank reason never reaches the API.
    fireEvent.click(screen.getByRole("button", { name: "تأیید ابطال" }));
    expect(await screen.findByText("دلیل ابطال را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/service-charges/${charge.id}/void`)).toHaveLength(0);

    fireEvent.change(screen.getByLabelText("دلیل ابطال"), { target: { value: "اشتباه بود" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید ابطال" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/service-charges/${charge.id}/void`)).toHaveLength(1),
    );
  });

  it("Box_NoChargeOnAClosedVisit_ShowsNothing", async () => {
    renderProfile(closedVisit(reza.id));

    expect(await screen.findByText("تاریخچه ورود و خروج")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "افزودن مبلغ هوازی" })).not.toBeInTheDocument();
  });
});
