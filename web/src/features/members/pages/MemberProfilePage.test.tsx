import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import {
  attendanceHistoryPage,
  autoClosedVisit,
  cancelledVisit,
  closedVisit,
  currentlyInsidePage,
  insideRow,
  openVisit,
  openVisitNoLocker,
} from "@/test/attendance";
import { planLabel } from "@/features/subscriptions/planLabel";
import { formatDate } from "@/lib/format";
import { cafePage, orderOnAccount } from "@/test/cafe";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import {
  ali,
  cafeDebtItem,
  debtItem,
  memberDebt,
  reza,
  serviceChargeDebtItem,
} from "@/test/members";
import {
  confirmMoneyReceived,
  confirmMoneyReturned,
  paymentHistoryItem,
  paymentOfActiveSubscription,
  paymentsPage,
  pickMethod,
} from "@/test/payments";
import { pricesNotSet, pricesResponse } from "@/test/prices";
import {
  activeSubscription,
  cancelledRenewal,
  expiredPaidSubscription,
  expiredUnpaidSubscription,
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

  it("Profile_NoOpenVisit_OffersNoCheckInAndPointsToTheLockerMap", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () =>
        attendanceHistoryPage([closedVisit(reza.id)]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    // Since 6.5.5 a member is let in only from the locker map, where the locker is chosen.
    expect(await screen.findByRole("link", { name: "ورود با کمد" })).toHaveAttribute("href", "/");
    expect(screen.queryByRole("button", { name: "ورود" })).not.toBeInTheDocument();
    expect(screen.queryByText("هم‌اکنون داخل باشگاه است.")).not.toBeInTheDocument();
  });

  // ---- Check-out and cancel: the same box as the locker map (BUSINESS_RULES.md §7) ----

  function profileHandlers(visits: ReturnType<typeof openVisit>[] = []) {
    return {
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage(visits),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt([]),
    };
  }

  it("Profile_OpenVisitOnAReservePlace_SaysItHasNoLocker", async () => {
    mockApi(profileHandlers([openVisitNoLocker(reza.id)]));

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("هم‌اکنون داخل باشگاه است.")).toBeInTheDocument();
    expect(screen.getByText(/· بدون کمد/)).toBeInTheDocument();
  });

  it("Profile_OpenVisit_ShowsCheckedInStateAndChecksOutAfterTheKey", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...profileHandlers([visit]),
      [`POST /api/attendance/${visit.id}/check-out`]: () =>
        json(200, { ...visit, checkedOutAt: "2026-09-18T09:00:00Z" }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("هم‌اکنون داخل باشگاه است.")).toBeInTheDocument();
    expect(screen.getByText(/کمد ۳/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "ثبت خروج" }));
    const dialog = await screen.findByRole("dialog");
    const confirm = within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" });
    expect(confirm).toBeDisabled();
    fireEvent.click(within(dialog).getByLabelText("کلید کمد شماره ۳ را تحویل گرفتم"));
    fireEvent.click(confirm);

    expect(await within(dialog).findByText("خروج ثبت شد")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/check-out`)).toHaveLength(1);
  });

  it("Profile_OpenVisit_CancelCheckInAsksFirstThenCancels", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...profileHandlers([visit]),
      // The box reads the visit's purchases from the "inside" list (roadmap 6.5.8).
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
      [`POST /api/attendance/${visit.id}/cancel`]: () =>
        json(200, {
          ...visit,
          checkedOutAt: "2026-09-18T07:05:00Z",
          cancelledAt: "2026-09-18T07:05:00Z",
        }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "لغو ورود" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("آیا از لغو ورود رضا احمدی مطمئن هستید؟");
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`)).toHaveLength(0);

    const confirm = within(dialog).getByRole("button", { name: "بله، ورود لغو شود" });
    await waitFor(() => expect(confirm).toBeEnabled());
    fireEvent.click(confirm);

    expect(await within(dialog).findByText("ورود لغو شد")).toBeInTheDocument();
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
    await waitFor(() => expect(screen.getAllByText("۳۰ روز · ۱۲ جلسه")).toHaveLength(2));
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
    // Not merely disabled: a row shows only the actions that are possible on it (task 4.7).
    expect(screen.queryByRole("button", { name: /^رفع فریز / })).not.toBeInTheDocument();
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

  it("Subscription_Assign_SendsTheTypedDaysAndSessionsAndShowsTheirPrice", async () => {
    // BUSINESS_RULES.md §3: the desk builds the plan, and its price is sessions × the session price.
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      "GET /api/pricing": () => pricesResponse(),
      [`POST /api/members/${reza.id}/subscriptions`]: () => json(201, activeSubscription),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "فروش اشتراک" }));
    // Persian digits in one box and English in the other: both are accepted everywhere.
    fireEvent.change(screen.getByLabelText("تعداد روز"), { target: { value: "۴۵" } });
    fireEvent.change(screen.getByLabelText("تعداد جلسات"), { target: { value: "12" } });

    // 12 × 75,000, shown before the sale is confirmed.
    expect(await screen.findByText(/۱۲ جلسه × ۷۵٬۰۰۰ تومان = ۹۰۰٬۰۰۰ تومان/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "تأیید فروش" }));

    expect(await screen.findByRole("status")).toHaveTextContent("اشتراک فروخته شد.");
    const sales = api.requestsTo("POST", `/api/members/${reza.id}/subscriptions`);
    expect(sales).toHaveLength(1);
    expect(await sales[0]!.clone().json()).toEqual({ durationDays: 45, sessionCount: 12 });
  });

  it("Subscription_AssignFewerThanFiveSessions_IsRefusedBeforeAnythingIsSent", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      "GET /api/pricing": () => pricesResponse(),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "فروش اشتراک" }));
    fireEvent.change(screen.getByLabelText("تعداد روز"), { target: { value: "30" } });
    fireEvent.change(screen.getByLabelText("تعداد جلسات"), { target: { value: "4" } });
    await waitFor(() => expect(screen.getByRole("button", { name: "تأیید فروش" })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: "تأیید فروش" }));

    expect(await screen.findByText("تعداد جلسات باید حداقل ۵ باشد.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${reza.id}/subscriptions`)).toHaveLength(0);
  });

  it("Subscription_AssignBeforeTheSessionPriceIsSet_SaysSoAndOffersNoSale", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      "GET /api/pricing": () => pricesResponse(pricesNotSet),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "فروش اشتراک" }));

    expect(await screen.findByText(/قیمت هر جلسه هنوز تعیین نشده است/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "تأیید فروش" })).toBeDisabled();
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
    fireEvent.click(await screen.findByRole("button", { name: "بله، فریز شود" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "همهٔ روزهای مجاز فریز این اشتراک استفاده شده است",
    );
  });

  it("Subscription_Freeze_AsksForConfirmationBeforeAnythingIsSent", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^فریز / }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent(/آیا از فریز .* مطمئن هستید؟/);
    expect(
      api.requestsTo("POST", `/api/subscriptions/${activeSubscription.id}/freeze`),
    ).toHaveLength(0);
  });

  it("Subscription_Freeze_ConfirmationShowsTheThirtyDayCapAndTheDaysLeft", async () => {
    // BUSINESS_RULES.md §4 Freeze: at most Gym:MaxFreezeDaysPerSubscription (30) days in total.
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () =>
        subscriptionsPage([{ ...activeSubscription, totalFrozenDays: 12 }]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^فریز / }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("هر اشتراک حداکثر ۳۰ روز فریز دارد.");
    expect(dialog).toHaveTextContent("تاکنون ۱۲ روز استفاده شده و ۱۸ روز باقی مانده است.");
  });

  it("Subscription_FreezeAnsweredNo_ClosesTheBoxAndSendsNothing", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^فریز / }));
    fireEvent.click(await screen.findByRole("button", { name: "خیر، برگرد" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(
      api.requestsTo("POST", `/api/subscriptions/${activeSubscription.id}/freeze`),
    ).toHaveLength(0);
  });

  it("Subscription_Unfreeze_AsksForConfirmationThenCallsTheApiAndShowsSuccess", async () => {
    const frozen = {
      ...activeSubscription,
      status: "Frozen" as const,
      frozenSince: activeSubscription.startDate,
    };
    const api = mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([frozen]),
      [`POST /api/subscriptions/${frozen.id}/unfreeze`]: () =>
        json(200, { ...activeSubscription, version: 2 }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^رفع فریز / }));
    const confirm = await screen.findByRole("button", { name: "بله، فریز برداشته شود" });
    expect(api.requestsTo("POST", `/api/subscriptions/${frozen.id}/unfreeze`)).toHaveLength(0);

    fireEvent.click(confirm);

    expect(await screen.findByRole("status")).toHaveTextContent("فریز اشتراک برداشته شد.");
    expect(api.requestsTo("POST", `/api/subscriptions/${frozen.id}/unfreeze`)).toHaveLength(1);
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
      name: `فریز ${planLabel(activeSubscription)} (${formatDate(activeSubscription.startDate)})`,
    });
    expect(activeRowFreeze).toBeEnabled();
    // The queued row cannot be frozen, so it offers no freeze button at all (task 4.7).
    expect(
      screen.queryByRole("button", {
        name: `فریز ${planLabel(queuedRenewal)} (${formatDate(queuedRenewal.startDate)})`,
      }),
    ).not.toBeInTheDocument();

    fireEvent.click(activeRowFreeze);
    fireEvent.click(await screen.findByRole("button", { name: "بله، فریز شود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("اشتراک فریز شد.");
    expect(
      api.requestsTo("POST", `/api/subscriptions/${activeSubscription.id}/freeze`),
    ).toHaveLength(1);
  });

  it("Subscription_Cancel_ByOwner_RequiresAReason", async () => {
    // A queued renewal: nobody has used it, so it is one of the few that can still be cancelled
    // (BUSINESS_RULES.md §4 Cancel).
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([queuedRenewal]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^لغو اشتراک / }));
    fireEvent.click(screen.getByRole("button", { name: "تأیید لغو" }));

    expect(await screen.findByText("دلیل لغو را وارد کنید.")).toBeInTheDocument();
  });

  it("Subscription_Refund_ExceedsNetPaid_ShowsThePersianError", async () => {
    // Unused and part-paid: the only shape a refund is offered for (BUSINESS_RULES.md §5).
    const unusedPartPaid = { ...activeSubscription, usedSessions: 0, remainingSessions: 12 };
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([unusedPartPaid]),
      [`POST /api/subscriptions/${activeSubscription.id}/refunds`]: () =>
        problem(422, "Payments.RefundExceedsNetPaid"),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^استرداد برای/ }));
    fireEvent.change(screen.getByLabelText("مبلغ استرداد (تومان)"), {
      target: { value: "500000" },
    });
    fireEvent.change(screen.getByLabelText("دلیل استرداد"), { target: { value: "دلیل" } });
    pickMethod(document.body, "Cash", "روش");
    fireEvent.click(screen.getByRole("button", { name: "تأیید استرداد" }));
    await confirmMoneyReturned();

    expect(
      await screen.findByText("این استرداد از مبلغ پرداخت‌شدهٔ این مورد بیشتر است."),
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
    pickMethod(document.body);
    fireEvent.click(screen.getByRole("button", { name: "تأیید پرداخت" }));
    await confirmMoneyReceived();

    expect(await screen.findByRole("status")).toHaveTextContent("پرداخت ثبت شد.");
    const request = api.requestsTo("POST", `/api/subscriptions/${activeSubscription.id}/payments`);
    expect(request).toHaveLength(1);
    expect(await request[0]!.clone().json()).toMatchObject({ amount: "400000", method: "Cash" });
  });

  it("History_RegisterPaymentWithNoMethodPicked_AsksForOneAndSendsNothing", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^ثبت پرداخت برای/ }));
    fireEvent.change(screen.getByLabelText("مبلغ (تومان)"), { target: { value: "400000" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید پرداخت" }));

    expect(await screen.findByText("روش پرداخت را انتخاب کنید.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(
      api.requestsTo("POST", `/api/subscriptions/${activeSubscription.id}/payments`),
    ).toHaveLength(0);
  });

  it("History_RegisterPaymentAnsweredNo_SendsNothing", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /^ثبت پرداخت برای/ }));
    fireEvent.change(screen.getByLabelText("مبلغ (تومان)"), { target: { value: "400000" } });
    pickMethod(document.body, "Card");
    fireEvent.click(screen.getByRole("button", { name: "تأیید پرداخت" }));

    const dialog = await screen.findByRole("dialog", { name: "آیا پول دریافت شد؟" });
    expect(dialog).toHaveTextContent("۴۰۰٬۰۰۰ تومان");
    fireEvent.click(within(dialog).getByRole("button", { name: "خیر، برگرد" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(
      api.requestsTo("POST", `/api/subscriptions/${activeSubscription.id}/payments`),
    ).toHaveLength(0);
    expect(screen.getByRole("button", { name: "تأیید پرداخت" })).toBeInTheDocument();
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

  it("History_SubscriptionAlreadyPaid_HidesItsPaymentButton", async () => {
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

    await screen.findByText(planLabel(paidSubscription), { selector: "td" });
    expect(screen.queryByRole("button", { name: /^ثبت پرداخت برای/ })).not.toBeInTheDocument();
  });

  // ---- Member debt (task 4.7) ----

  it("Debt_MemberWhoOwesMoney_ShowsTheTotalAndOpensTheBreakdown", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () =>
        memberDebt([
          debtItem(),
          debtItem({
            id: "0199a000-0000-7000-8000-0000000000b3",
            plan: { durationDays: 90, totalSessions: 36, isSingleSession: false },
            price: 500000,
            netPaid: 0,
            outstanding: 500000,
          }),
        ]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    // The total on its own first; the items only after asking for them (BUSINESS_RULES.md §5).
    expect(await screen.findByText("۱٬۱۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    expect(screen.queryByText("اشتراک ۹۰ روز · ۳۶ جلسه")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "جزء به جزء" }));

    // Each item says what it is, what it cost, what has been paid and what is left.
    const monthlyRow = (await screen.findByText("اشتراک ۳۰ روز · ۱۲ جلسه")).closest("tr")!;
    expect(within(monthlyRow).getByText("۹۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    expect(within(monthlyRow).getByText("۳۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    expect(within(monthlyRow).getByText("۶۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    const quarterlyRow = screen.getByText("اشتراک ۹۰ روز · ۳۶ جلسه").closest("tr")!;
    expect(within(quarterlyRow).getAllByText("۵۰۰٬۰۰۰ تومان")).toHaveLength(2);
  });

  /**
   * BUSINESS_RULES.md §5 Member debt: a service charge joins the same total, and the breakdown
   * labels it by its kind — the API sends "Cardio" and never Persian text (roadmap 5.7).
   */
  it("Debt_MemberWhoOwesForCardio_ShowsItInTheBreakdown", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt([debtItem(), serviceChargeDebtItem()]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("۶۱۰٬۰۰۰ تومان")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "جزء به جزء" }));

    const cardioRow = (await screen.findByText("هوازی")).closest("tr")!;
    expect(within(cardioRow).getAllByText("۱۰٬۰۰۰ تومان")).toHaveLength(2);
  });

  it("Debt_MemberWhoOwesTheCafe_ListsWhatWasBoughtUnderTheCafeRowWithPrices", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () =>
        memberDebt([
          cafeDebtItem({
            price: 70000,
            outstanding: 70000,
            cafeItems: [
              {
                id: "0199a000-0000-7000-8000-0000000000c1",
                productId: "0199a000-0000-7000-8000-0000000000c2",
                productName: "آب معدنی",
                unitPrice: 15000,
                quantity: 2,
                lineTotal: 30000,
              },
              {
                id: "0199a000-0000-7000-8000-0000000000c3",
                productId: "0199a000-0000-7000-8000-0000000000c4",
                productName: "کیک",
                unitPrice: 40000,
                quantity: 1,
                lineTotal: 40000,
              },
            ],
          }),
        ]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("۷۰٬۰۰۰ تومان")).toBeInTheDocument();
    expect(screen.queryByText("آب معدنی × ۲")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "جزء به جزء" }));

    // Each thing bought sits on its own row under «بوفه», with what it cost.
    const waterRow = (await screen.findByText("آب معدنی × ۲")).closest("tr")!;
    expect(within(waterRow).getByText("۳۰٬۰۰۰ تومان")).toBeInTheDocument();
    const cakeRow = screen.getByText("کیک × ۱").closest("tr")!;
    expect(within(cakeRow).getByText("۴۰٬۰۰۰ تومان")).toBeInTheDocument();
    expect(waterRow.previousElementSibling).toHaveTextContent("بوفه");
  });

  it("Debt_MemberWhoOwesNothing_SaysSo", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt([]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("این عضو بدهی ندارد.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "جزء به جزء" })).not.toBeInTheDocument();
  });

  // ---- Which actions a row offers (task 4.7) ----

  /**
   * "Debt outlives the thing that created it" (BUSINESS_RULES.md §5 Member debt): an expired
   * subscription with money still owed keeps taking payments, whatever its status.
   */
  it("History_FinishedButUnpaidSubscription_StillOffersToTakeAPayment", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () =>
        subscriptionsPage([expiredUnpaidSubscription]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByRole("button", { name: /^ثبت پرداخت برای/ })).toBeEnabled();
  });

  it("History_FinishedAndSettledSubscription_OffersNoActionsAtAll", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () =>
        subscriptionsPage([expiredPaidSubscription]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    const row = (
      await screen.findByText(planLabel(expiredPaidSubscription), { selector: "td" })
    ).closest("tr")!;
    expect(within(row).queryAllByRole("button")).toHaveLength(0);
  });

  it("History_UsedSubscription_OffersNeitherCancelNorRefund", async () => {
    // activeSubscription has three used sessions: a service consumed is not un-sold, and the
    // money for it is not bought back (BUSINESS_RULES.md §4 Cancel, §5).
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    await screen.findByRole("button", { name: /^فریز / });
    expect(screen.queryByRole("button", { name: /^لغو اشتراک / })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^استرداد برای/ })).not.toBeInTheDocument();
  });

  it("History_UnusedSubscription_OffersCancelToTheOwner", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([queuedRenewal]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByRole("button", { name: /^لغو اشتراک / })).toBeEnabled();
    // Nothing has been paid on it, so there is nothing to refund either.
    expect(screen.queryByRole("button", { name: /^استرداد برای/ })).not.toBeInTheDocument();
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

  it("History_SwitchToCafeTab_ListsTheMembersPurchases", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/cafe-orders`]: () => cafePage([orderOnAccount]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    await screen.findByRole("button", { name: "فروش اشتراک" });
    fireEvent.click(screen.getByRole("tab", { name: "خریدهای بوفه" }));

    expect(await screen.findByText("شیک پروتئین × ۱")).toBeInTheDocument();
    // Every row is this member's, so the customer column is left out.
    expect(screen.queryByRole("columnheader", { name: "مشتری" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^ثبت پرداخت برای سفارش/ })).toBeInTheDocument();
  });

  it("Profile_CafeButton_OpensTheTillWithThisMemberChosen", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByRole("link", { name: "خرید از بوفه" })).toHaveAttribute(
      "href",
      `/cafe?member=${reza.id}`,
    );
  });
});
