import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import {
  attendanceHistoryPage,
  cardioCharge,
  closedVisit,
  currentlyInsidePage,
  insideRow,
  openVisit,
} from "@/test/attendance";
import { cafePage } from "@/test/cafe";
import { allLockers, heldLocker, lockerId, lockersPage } from "@/test/lockers";
import {
  json,
  mockApi,
  problem,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import { memberDebt, reza } from "@/test/members";
import { confirmMoneyReceived, pickMethod } from "@/test/payments";
import { renderApp } from "@/test/renderApp";
import { subscriptionsPage } from "@/test/subscriptions";
import type { Attendance } from "@/features/attendance/api";
import type { ServiceCharge } from "@/features/serviceCharges/api";

/**
 * The هوازی slot of a visit (BUSINESS_RULES.md §7 Gym services). Rendered through the page that
 * holds it rather than on its own, because what the front desk can do to a charge depends on the
 * visit it hangs off: an open visit's box on its locker, and a closed visit's row in the profile's
 * history.
 *
 * An existing charge shows only its amount and payment badge; the actions and their forms live in
 * a dialog opened from it (task 6.5.2), so these tests open it first. That is the point of the
 * change: the row this sits in must stay one line.
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

  /** Reza inside on locker 2, the only place an open visit's هوازی is typed. */
  function onLocker(visit: Attendance): Attendance {
    return { ...visit, lockerId: lockerId(2), lockerNumber: 2 };
  }

  /** Opens locker 2's box on the map; `visit` is read on every request, so a test can change it. */
  async function renderLocker(visit: () => Attendance, extra: Record<string, Handler> = {}) {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/lockers": () => lockersPage(allLockers(heldLocker(2, reza.id, reza.fullName))),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit())]),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt(),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      "GET /api/cafe/orders": () => cafePage([]),
      ...extra,
    });
    renderApp("/", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: /^کمد ۲،/ }));
    await screen.findByRole("dialog");

    return api;
  }

  function withCharge(visit: Attendance, overrides: Partial<ServiceCharge> = {}): Attendance {
    return { ...visit, serviceCharges: [cardioCharge(visit, overrides)] };
  }

  /** The row shows a summary; the actions are behind it. Its label carries the amount. */
  async function openChargeDialog() {
    fireEvent.click(await screen.findByRole("button", { name: /^هوازی:/ }));
  }

  it("Box_OpenVisitWithNoCharge_OffersToAddOne", async () => {
    const visit = onLocker(openVisit(reza.id));
    await renderLocker(() => visit);

    expect(await screen.findByRole("button", { name: "هوازی" })).toBeInTheDocument();
  });

  /**
   * The amount in words is the guard against a miscounted zero (BUSINESS_RULES.md §13), so the
   * هوازی box has to be the shared MoneyField and not a plain input.
   */
  it("Box_TypingAnAmount_ShowsItInPersianWordsAndPostsIt", async () => {
    const visit = onLocker(openVisit(reza.id));
    const api = await renderLocker(() => visit, {
      [`POST /api/attendance/${visit.id}/service-charges`]: () =>
        json(201, cardioCharge(visit, { amount: 10000 })),
    });

    fireEvent.click(await screen.findByRole("button", { name: "هوازی" }));
    fireEvent.change(screen.getByLabelText("مبلغ هوازی"), { target: { value: "10000" } });

    expect(screen.getByLabelText("مبلغ هوازی")).toHaveValue("۱۰٬۰۰۰");
    expect(screen.getByText("ده هزار تومان")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "ثبت" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/attendance/${visit.id}/service-charges`)).toHaveLength(1),
    );
  });

  /**
   * The dialog does not just vanish: it says the amount was saved, and which amount, until the
   * desk closes it. The success step has to survive the refetch that turns "no charge" into a
   * charge behind it.
   */
  it("Box_AmountSaved_ShowsSuccessWithTheAmountUntilClosed", async () => {
    const visit = onLocker(openVisit(reza.id));
    let saved = false;
    await renderLocker(() => (saved ? withCharge(visit, { amount: 50000 }) : visit), {
      [`POST /api/attendance/${visit.id}/service-charges`]: () => {
        saved = true;
        return json(201, cardioCharge(visit, { amount: 50000 }));
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "هوازی" }));
    fireEvent.change(screen.getByLabelText("مبلغ هوازی"), { target: { value: "50000" } });
    fireEvent.click(screen.getByRole("button", { name: "ثبت" }));

    const dialog = await screen.findByRole("dialog", { name: "مبلغ هوازی ثبت شد" });
    expect(within(dialog).getByText("۵۰٬۰۰۰ تومان")).toBeInTheDocument();

    fireEvent.click(within(dialog).getAllByRole("button", { name: "بستن" })[0]!);
    await waitFor(() => expect(dialog).not.toBeInTheDocument());
    expect(await screen.findByRole("button", { name: "هوازی: ۵۰٬۰۰۰ تومان" })).toBeInTheDocument();
  });

  it("Box_ServerRefusesTheAmount_ShowsThePersianMessageOnTheField", async () => {
    const visit = onLocker(openVisit(reza.id));
    await renderLocker(() => visit, {
      [`POST /api/attendance/${visit.id}/service-charges`]: () =>
        problem(409, "ServiceCharges.AlreadyCharged"),
    });

    fireEvent.click(await screen.findByRole("button", { name: "هوازی" }));
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
    const visit = withCharge(onLocker(openVisit(reza.id)));
    await renderLocker(() => visit);

    await openChargeDialog();

    expect(await screen.findByRole("button", { name: "ویرایش مبلغ" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "ثبت پرداخت" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "ابطال" })).toBeInTheDocument();
    expect(screen.getByText("مانده: ۱۰٬۰۰۰ تومان")).toBeInTheDocument();
  });

  /** Like every payment (BUSINESS_RULES.md §5): a method is picked and the money confirmed first. */
  it("Box_TakingThePayment_AsksWhetherTheMoneyWasReceivedBeforeSending", async () => {
    const visit = onLocker(openVisit(reza.id));
    const charge = cardioCharge(visit);
    const paymentPath = `/api/service-charges/${charge.id}/payments`;
    const api = await renderLocker(() => withCharge(visit), {
      [`POST ${paymentPath}`]: () => json(201, { id: "payment" }),
    });

    await openChargeDialog();
    fireEvent.click(await screen.findByRole("button", { name: "ثبت پرداخت" }));
    fireEvent.change(screen.getByLabelText("مبلغ (تومان)"), { target: { value: "10000" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید پرداخت" }));

    expect(await screen.findByText("روش پرداخت را انتخاب کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", paymentPath)).toHaveLength(0);

    pickMethod(document.body, "Card");
    fireEvent.click(screen.getByRole("button", { name: "تأیید پرداخت" }));
    expect(await screen.findByRole("dialog", { name: "آیا پول دریافت شد؟" })).toBeInTheDocument();
    expect(api.requestsTo("POST", paymentPath)).toHaveLength(0);

    await confirmMoneyReceived();
    await waitFor(() => expect(api.requestsTo("POST", paymentPath)).toHaveLength(1));
    expect(await api.requestsTo("POST", paymentPath)[0]!.json()).toEqual({
      amount: "10000",
      method: "Card",
      referenceNumber: null,
    });
  });

  /**
   * BUSINESS_RULES.md §7: once money has been taken it is a financial record, corrected with a
   * void and a reason. The button that would edit it is gone rather than disabled — the same
   * decision as the subscription history row in task 4.7.
   */
  it("Box_PaidCharge_OffersOnlyVoid", async () => {
    const visit = withCharge(onLocker(openVisit(reza.id)), {
      netPaid: 10000,
      paymentStatus: "Paid",
      canChangeAmount: false,
    });
    await renderLocker(() => visit);

    await openChargeDialog();

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

    await openChargeDialog();

    expect(await screen.findByRole("button", { name: "ثبت پرداخت" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ویرایش مبلغ" })).not.toBeInTheDocument();
  });

  /** A settled charge on a finished visit offers nothing but the void that undoes it. */
  it("Box_PaidChargeOnAClosedVisit_OffersOnlyVoid", async () => {
    const visit = closedVisit(reza.id);
    renderProfile(
      withCharge(visit, { netPaid: 10000, paymentStatus: "Paid", canChangeAmount: false }),
    );

    await openChargeDialog();

    expect(await screen.findByRole("button", { name: "ابطال" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ثبت پرداخت" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ویرایش مبلغ" })).not.toBeInTheDocument();
  });

  it("Box_Voiding_RequiresAReasonAndWarnsThatMoneyComesBack", async () => {
    const visit = onLocker(openVisit(reza.id));
    const charge = cardioCharge(visit, {
      netPaid: 10000,
      paymentStatus: "Paid",
      canChangeAmount: false,
    });
    const api = await renderLocker(() => ({ ...visit, serviceCharges: [charge] }), {
      [`POST /api/service-charges/${charge.id}/void`]: () =>
        json(200, { ...charge, voidedAt: "2026-09-18T07:30:00Z", voidReason: "اشتباه بود" }),
    });

    await openChargeDialog();
    fireEvent.click(await screen.findByRole("button", { name: "ابطال" }));

    expect(
      screen.getByText(
        "مبلغی که برای این مورد پرداخت شده است، با همین دلیل به عضو بازگردانده می‌شود.",
      ),
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
    expect(await screen.findByText("مبلغ هوازی ابطال شد")).toBeInTheDocument();
  });

  it("Box_NoChargeOnAClosedVisit_ShowsNothing", async () => {
    renderProfile(closedVisit(reza.id));

    expect(await screen.findByText("تاریخچه ورود و خروج")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "مبلغ هوازی" })).not.toBeInTheDocument();
  });
});
