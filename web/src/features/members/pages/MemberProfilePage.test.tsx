import { fireEvent, screen, waitFor } from "@testing-library/react";

import {
  attendanceHistoryPage,
  autoClosedVisit,
  cancelledVisit,
  closedVisit,
  openVisit,
  openVisitNoLocker,
} from "@/test/attendance";
import { formatDate } from "@/lib/format";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { ali, reza } from "@/test/members";
import { paymentHistoryItem, paymentOfActiveSubscription, paymentsPage } from "@/test/payments";
import { monthly12, plansPage } from "@/test/plans";
import {
  activeSubscription,
  cancelledRenewal,
  queuedRenewal,
  subscriptionsPage,
} from "@/test/subscriptions";
import { renderApp } from "@/test/renderApp";

describe("MemberProfilePage", () => {
  it("Profile_Loaded_ShowsNamePhoneNotesAndStatus", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("رضا احمدی")).toBeInTheDocument();
    expect(screen.getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toHaveAttribute("dir", "ltr");
    expect(screen.getByText("عضو قدیمی")).toBeInTheDocument();
    expect(screen.getByText("۱۳۷۰/۰۵/۱۲")).toBeInTheDocument();
    expect(screen.getByText("فعال")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ویرایش" })).toHaveAttribute(
      "href",
      `/members/${reza.id}/edit`,
    );
  });

  it("Profile_MemberWithoutABirthDate_ShowsADash", async () => {
    // Most members have none (docs/BUSINESS_RULES.md §2), so the empty case is the common one.
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${ali.id}`]: () => json(200, ali),
      [`GET /api/members/${ali.id}/attendance`]: () => attendanceHistoryPage([]),
      [`GET /api/members/${ali.id}/subscriptions`]: () => subscriptionsPage([]),
    });

    renderApp(`/members/${ali.id}`, { session: session() });

    expect(await screen.findByText("علی رضایی")).toBeInTheDocument();
    const birthDate = screen.getByText("تاریخ تولد").nextElementSibling;
    expect(birthDate).toHaveTextContent("—");
  });

  it("Profile_Deactivate_CallsTheApiAndShowsTheMemberAsInactive", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      [`POST /api/members/${reza.id}/deactivate`]: () =>
        json(200, { ...reza, isActive: false, version: 6 }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "غیرفعال‌سازی" }));

    expect(await screen.findByRole("status")).toHaveTextContent("عضو غیرفعال شد.");
    expect(screen.getByText("غیرفعال")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "فعال‌سازی" })).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${reza.id}/deactivate`)).toHaveLength(1);
  });

  it("Profile_Reactivate_CallsTheApiAndShowsTheMemberAsActive", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${ali.id}`]: () => json(200, ali),
      [`GET /api/members/${ali.id}/attendance`]: () => attendanceHistoryPage([]),
      [`GET /api/members/${ali.id}/subscriptions`]: () => subscriptionsPage([]),
      [`POST /api/members/${ali.id}/reactivate`]: () =>
        json(200, { ...ali, isActive: true, version: 8 }),
    });
    renderApp(`/members/${ali.id}`, { session: session() });

    expect(await screen.findByText(/نمی‌تواند وارد باشگاه شود/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "فعال‌سازی" }));

    expect(await screen.findByRole("status")).toHaveTextContent("عضو دوباره فعال شد.");
    expect(screen.getByText("فعال")).toBeInTheDocument();
    expect(screen.queryByText(/نمی‌تواند وارد باشگاه شود/)).not.toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${ali.id}/reactivate`)).toHaveLength(1);
  });

  it("Profile_DeactivateFails_ShowsThePersianError", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      [`POST /api/members/${reza.id}/deactivate`]: () =>
        problem(409, "Members.ChangedConcurrently"),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "غیرفعال‌سازی" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("هم‌زمان توسط شخص دیگری ویرایش شد");
  });

  it("Profile_UnknownId_SaysTheMemberWasNotFoundWithoutRetrying", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => problem(404, "Members.NotFound"),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("عضو پیدا نشد.")).toBeInTheDocument();
    expect(api.requestsTo("GET", `/api/members/${reza.id}`)).toHaveLength(1);
  });

  // ---- Attendance (docs/ROADMAP.md 5.6) ----

  it("Profile_NoOpenVisit_ShowsACheckInButton", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () =>
        attendanceHistoryPage([closedVisit(reza.id)]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByRole("button", { name: "ورود" })).toBeInTheDocument();
    expect(screen.queryByText("هم‌اکنون داخل باشگاه است.")).not.toBeInTheDocument();
  });

  it("Profile_CheckIn_ShowsTheAssignedLocker", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () => json(201, visit),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("کمد شماره ۳");
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(1);
  });

  it("Profile_CheckInWithNoFreeLocker_ShowsTheWarning", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        json(201, openVisitNoLocker(reza.id)),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("کمد آزادی نبود");
  });

  it("Profile_CheckInFails_ShowsThePersianReason", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        problem(422, "Attendance.NoSubscription"),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("این عضو اشتراکی ندارد.");
  });

  it("Profile_OpenVisit_ShowsCheckedInStateAndCanCheckOut", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([visit]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      [`POST /api/attendance/${visit.id}/check-out`]: () =>
        json(200, { ...visit, checkedOutAt: "2026-09-18T09:00:00Z" }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("هم‌اکنون داخل باشگاه است.")).toBeInTheDocument();
    expect(screen.getByText(/کمد ۳/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "ثبت خروج" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/attendance/${visit.id}/check-out`)).toHaveLength(1),
    );
  });

  it("Profile_OpenVisit_CanCancelCheckIn", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([visit]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      [`POST /api/attendance/${visit.id}/cancel`]: () =>
        json(200, {
          ...visit,
          checkedOutAt: "2026-09-18T07:05:00Z",
          cancelledAt: "2026-09-18T07:05:00Z",
        }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "لغو ورود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("جلسه به اشتراک بازگشت");
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`)).toHaveLength(1);
  });

  it("Profile_History_ShowsCancelledAndAutoClosedStatuses", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () =>
        attendanceHistoryPage([cancelledVisit(reza.id), autoClosedVisit(reza.id)]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("لغو شده")).toBeInTheDocument();
    expect(screen.getByText("بسته خودکار")).toBeInTheDocument();
  });

  // ---- Current subscription card (task 4.6) ----

  it("Subscription_MemberHasOne_ShowsPlanStatusDatesSessionsAndPaymentStatus", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    // Every label below is shown twice: once on the current-subscription card, once on the same
    // subscription's row in the history tab underneath it (the default tab). The two sections
    // load independently, so wait for both instead of racing on whichever resolves first.
    await waitFor(() => expect(screen.getAllByText("یک ماهه ۱۲ جلسه")).toHaveLength(2));
    expect(screen.getAllByText("فعال").length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByText("پرداخت جزئی").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText(/۴۰۰٬۰۰۰ تومان از ۹۰۰٬۰۰۰ تومان/)).toBeInTheDocument();
  });

  it("Subscription_ActiveWithANewerCancelledOne_ShowsTheActiveOneAsCurrent", async () => {
    // Bug found by the developer: the card used to show whichever subscription had the latest
    // start date, so a cancel-then-resell history hid the still-active subscription behind a
    // cancelled one that merely happened to be sold more recently.
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () =>
        subscriptionsPage([cancelledRenewal, activeSubscription]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    // The active subscription's start date appears twice (the card and its own history row); the
    // newer but cancelled one appears only once (its own row, never promoted to the card).
    await waitFor(() =>
      expect(screen.getAllByText(formatDate(activeSubscription.startDate))).toHaveLength(2),
    );
    expect(screen.getAllByText(formatDate(cancelledRenewal.startDate))).toHaveLength(1);
  });

  it("Subscription_MemberHasNone_SaysSoAndDisablesRenew", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("این عضو هنوز اشتراکی ندارد.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "تمدید" })).toBeDisabled();
  });

  it("Subscription_StaffUser_DoesNotSeeOwnerOnlyActions", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    await screen.findByRole("button", { name: "فروش اشتراک" });
    expect(screen.queryByRole("button", { name: /^فریز / })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^لغو اشتراک / })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^استرداد برای/ })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^ثبت پرداخت برای/ })).toBeInTheDocument();
  });

  it("Subscription_OwnerUser_SeesFreezeOnlyWhenActive", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    await screen.findByRole("button", { name: "فروش اشتراک" });
    expect(screen.getByRole("button", { name: /^فریز / })).toBeEnabled();
    expect(screen.getByRole("button", { name: /^رفع فریز / })).toBeDisabled();
  });

  it("Subscription_Renew_AsksForConfirmationThenCallsTheApiAndShowsSuccess", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`POST /api/members/${reza.id}/subscriptions/renew`]: () => json(201, activeSubscription),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "تمدید" }));
    // Opening the confirmation panel must not call the API by itself — this is the bug being
    // fixed: a click on "تمدید" used to call renew immediately, so repeated clicks each queued
    // another subscription.
    const confirmButton = await screen.findByRole("button", { name: "تأیید تمدید" });
    expect(api.requestsTo("POST", `/api/members/${reza.id}/subscriptions/renew`)).toHaveLength(0);

    fireEvent.click(confirmButton);

    expect(await screen.findByRole("status")).toHaveTextContent(/^اشتراک تمدید شد/);
    expect(api.requestsTo("POST", `/api/members/${reza.id}/subscriptions/renew`)).toHaveLength(1);
  });

  it("Subscription_Assign_SellsChosenPlanAndShowsSuccess", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      "GET /api/plans": () => plansPage([monthly12]),
      [`POST /api/members/${reza.id}/subscriptions`]: () => json(201, activeSubscription),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "فروش اشتراک" }));
    // Waits for the plan list itself to load, not just the (already-rendered) empty select.
    await screen.findByRole("option", { name: new RegExp(monthly12.name) });
    fireEvent.change(screen.getByLabelText("پلن"), { target: { value: monthly12.id } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید فروش" }));

    expect(await screen.findByRole("status")).toHaveTextContent("اشتراک فروخته شد.");
    expect(api.requestsTo("POST", `/api/members/${reza.id}/subscriptions`)).toHaveLength(1);
  });

  it("Subscription_Freeze_Returns422AndShowsThePersianError", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`POST /api/subscriptions/${activeSubscription.id}/freeze`]: () =>
        problem(422, "Subscriptions.FreezeLimitReached"),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^فریز / }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "همهٔ روزهای مجاز فریز این اشتراک استفاده شده است",
    );
  });

  it("Subscription_ActiveWithAQueuedRenewal_CanStillFreezeTheActiveOneFromItsRow", async () => {
    // The current-subscription card always shows the newest subscription by start date — right
    // after a renewal, that is the queued one, not the still-active one underneath it. Freezing
    // used to be reachable only through that card, so it silently became impossible for the
    // active subscription the moment a renewal existed. This is the scenario the developer found.
    const api = mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () =>
        subscriptionsPage([queuedRenewal, activeSubscription]),
      [`POST /api/subscriptions/${activeSubscription.id}/freeze`]: () =>
        json(200, {
          ...activeSubscription,
          status: "Frozen",
          frozenSince: activeSubscription.startDate,
          version: 2,
        }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    // The card's only subscription-level action left is "تمدید"; it shows the queued renewal.
    await screen.findByRole("button", { name: "تمدید" });

    const activeRowFreeze = screen.getByRole("button", {
      name: `فریز ${activeSubscription.planName} (${formatDate(activeSubscription.startDate)})`,
    });
    const queuedRowFreeze = screen.getByRole("button", {
      name: `فریز ${queuedRenewal.planName} (${formatDate(queuedRenewal.startDate)})`,
    });
    expect(activeRowFreeze).toBeEnabled();
    expect(queuedRowFreeze).toBeDisabled();

    fireEvent.click(activeRowFreeze);

    expect(await screen.findByRole("status")).toHaveTextContent("اشتراک فریز شد.");
    expect(
      api.requestsTo("POST", `/api/subscriptions/${activeSubscription.id}/freeze`),
    ).toHaveLength(1);
  });

  it("Subscription_Cancel_ByOwner_RequiresAReason", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^لغو اشتراک / }));
    fireEvent.click(screen.getByRole("button", { name: "تأیید لغو" }));

    expect(await screen.findByText("دلیل لغو را وارد کنید.")).toBeInTheDocument();
  });

  it("Subscription_Refund_ExceedsNetPaid_ShowsThePersianError", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`POST /api/subscriptions/${activeSubscription.id}/refunds`]: () =>
        problem(422, "Payments.RefundExceedsNetPaid"),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^استرداد برای/ }));
    fireEvent.change(screen.getByLabelText("مبلغ استرداد (تومان)"), {
      target: { value: "500000" },
    });
    fireEvent.change(screen.getByLabelText("دلیل استرداد"), { target: { value: "دلیل" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید استرداد" }));

    expect(
      await screen.findByText("این استرداد از مبلغ پرداخت‌شدهٔ اشتراک بیشتر است."),
    ).toBeInTheDocument();
  });

  // ---- History tabs (task 4.6) ----

  it("History_RegisterPaymentForListedSubscription_CallsTheApiAndShowsSuccess", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`POST /api/subscriptions/${activeSubscription.id}/payments`]: () =>
        json(201, paymentOfActiveSubscription),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^ثبت پرداخت برای/ }));
    fireEvent.change(screen.getByLabelText("مبلغ (تومان)"), { target: { value: "400000" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید پرداخت" }));

    expect(await screen.findByRole("status")).toHaveTextContent("پرداخت ثبت شد.");
    const request = api.requestsTo("POST", `/api/subscriptions/${activeSubscription.id}/payments`);
    expect(request).toHaveLength(1);
    expect(await request[0]!.clone().json()).toMatchObject({ amount: "400000", method: "Cash" });
  });

  it("History_RegisterPayment_AmountFieldGroupsDigitsAsYouType", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^ثبت پرداخت برای/ }));
    const amountInput = screen.getByLabelText("مبلغ (تومان)") as HTMLInputElement;
    fireEvent.change(amountInput, { target: { value: "2000000" } });

    expect(amountInput.value).toBe("۲٬۰۰۰٬۰۰۰");
  });

  it("History_SubscriptionAlreadyPaid_DisablesItsPaymentButton", async () => {
    const paidSubscription = {
      ...activeSubscription,
      netPaid: activeSubscription.price,
      paymentStatus: "Paid" as const,
    };
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([paidSubscription]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByRole("button", { name: /^ثبت پرداخت برای/ })).toBeDisabled();
  });

  it("History_SwitchToPaymentsTab_LoadsAndShowsPayments", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/payments`]: () => paymentsPage([paymentHistoryItem]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    await screen.findByRole("button", { name: "فروش اشتراک" });
    fireEvent.click(screen.getByRole("tab", { name: "پرداخت‌ها" }));

    expect(await screen.findByText("۴۰۰٬۰۰۰ تومان")).toBeInTheDocument();
  });
});
