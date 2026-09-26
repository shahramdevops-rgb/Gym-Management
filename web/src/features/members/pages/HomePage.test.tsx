import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { Member } from "@/features/members/api";
import {
  attendanceHistoryPage,
  closedVisit,
  openVisit,
  openVisitNoLocker,
} from "@/test/attendance";
import {
  json,
  mockApi,
  problem,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import {
  ali,
  debtItem,
  memberDebt,
  membersPage,
  queryOf,
  reza,
  serviceChargeDebtItem,
} from "@/test/members";
import { plansPage, singleSession } from "@/test/plans";
import { renderApp } from "@/test/renderApp";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";

// Every test signs in as Staff: finding members is front-desk work (BUSINESS_RULES.md §1).
function searchBox() {
  return screen.getByRole("searchbox", { name: "نام یا شماره موبایل" });
}

describe("HomePage", () => {
  it("Search_TypedWithArabicYe_SendsOneNormalizedRequestAfterThePause", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([ali]),
    });
    renderApp("/", { session: session() });

    // Three quick key presses, the last with the Arabic ye (U+064A).
    fireEvent.change(searchBox(), { target: { value: "عل" } });
    fireEvent.change(searchBox(), { target: { value: "عل\u064A" } });
    fireEvent.change(searchBox(), { target: { value: "عل\u064A " } });

    expect(await screen.findByRole("link", { name: "علی رضایی" })).toBeInTheDocument();
    const requests = api.requestsTo("GET", "/api/members");
    expect(requests).toHaveLength(1);
    expect(queryOf(requests[0]!).get("Search")).toBe("علی");
  });

  it("Search_PhoneWithPersianDigits_SendsEnglishDigits", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
    });
    renderApp("/", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "۰۹۱۲ ۱۲۳ ۴۵۶۷" } });

    const row = (await screen.findByRole("link", { name: "رضا احمدی" })).closest("tr")!;
    expect(within(row).getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toHaveAttribute("dir", "ltr");
    expect(queryOf(api.requestsTo("GET", "/api/members")[0]!).get("Search")).toBe("0912 123 4567");
  });

  it("Search_EnterKey_SearchesWithoutWaiting", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
    });
    const { router } = renderApp("/", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "رضا" } });
    fireEvent.submit(screen.getByRole("search"));

    // Straight into the URL, not after the debounce.
    expect(router.state.location.search).toBe(`?q=${encodeURIComponent("رضا")}`);
    await screen.findByRole("link", { name: "رضا احمدی" });
    expect(api.requestsTo("GET", "/api/members")).toHaveLength(1);
  });

  it("Search_OneCharacter_SendsNothingAndShowsAHint", async () => {
    const api = mockApi(signedInHandlers(staffUser));
    renderApp("/", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "ع" } });

    expect(await screen.findByText("برای جستجو دست‌کم ۲ حرف وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/members")).toHaveLength(0);
  });

  it("Search_NobodyMatches_SaysSo", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([]) });
    renderApp("/", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "ناشناس" } });

    expect(await screen.findByText("عضوی با این مشخصات پیدا نشد.")).toBeInTheDocument();
  });

  it("Search_InTheUrl_IsRestoredAfterAReload", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
    });

    // A reload, or "back" from a profile, arrives with the search already in the URL.
    renderApp(`/?q=${encodeURIComponent("رضا")}`, { session: session() });

    expect(await screen.findByRole("link", { name: "رضا احمدی" })).toBeInTheDocument();
    expect(searchBox()).toHaveValue("رضا");
    expect(queryOf(api.requestsTo("GET", "/api/members")[0]!).get("Search")).toBe("رضا");
  });

  it("Search_ClickingAResult_OpensTheProfile", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
      [`GET /api/members/${reza.id}`]: () => new Response(JSON.stringify(reza)),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
    });
    const { router } = renderApp(`/?q=${encodeURIComponent("رضا")}`, { session: session() });

    fireEvent.click(await screen.findByRole("link", { name: "رضا احمدی" }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/members/${reza.id}`));
    expect(await screen.findByText("عضو قدیمی")).toBeInTheDocument();
  });

  it("Search_ManyResults_PagesWithPersianNumbers", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza], 45),
    });
    renderApp(`/?q=${encodeURIComponent("رضا")}`, { session: session() });

    expect(await screen.findByText("صفحهٔ ۱ از ۳")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "بعدی" }));

    await waitFor(() => expect(api.requestsTo("GET", "/api/members")).toHaveLength(2));
    const second = queryOf(api.requestsTo("GET", "/api/members")[1]!);
    expect(second.get("Page")).toBe("2");
    expect(second.get("Search")).toBe("رضا");
  });

  // ---- The check-in and check-out box (BUSINESS_RULES.md §7 Confirming at the front desk) ----

  const visitId = openVisit(reza.id).id;

  /** Reza as the search sees him while he is inside, in locker ۱۰. */
  const rezaInside: Member = {
    ...reza,
    currentVisit: { attendanceId: visitId, lockerNumber: 10, checkedInAt: "2026-09-18T07:00:00Z" },
  };

  /** What the box reads about the member besides the action itself: the plan and the debt. */
  function deskHandlers(member: Member, extra: Record<string, Handler> = {}) {
    return {
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([member]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt([]),
      ...extra,
    };
  }

  function renderSearch() {
    return renderApp("/?q=" + encodeURIComponent("رضا"), { session: session() });
  }

  async function rowOf(name: string) {
    return (await screen.findByRole("link", { name })).closest("tr")!;
  }

  async function pressCheckIn() {
    fireEvent.click(within(await rowOf("رضا احمدی")).getByRole("button", { name: "ورود" }));
    return screen.findByRole("dialog");
  }

  async function confirmCheckIn() {
    const dialog = await pressCheckIn();
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، ورود ثبت شود" }));
    return dialog;
  }

  it("Row_MemberOutside_OffersCheckInAndGreysOutCheckOut", async () => {
    mockApi(deskHandlers(reza));
    renderSearch();

    const row = await rowOf("رضا احمدی");

    expect(within(row).getByRole("button", { name: "ورود" })).toBeEnabled();
    expect(within(row).getByRole("button", { name: "خروج" })).toBeDisabled();
    expect(within(row).queryByText("داخل باشگاه")).not.toBeInTheDocument();
  });

  it("Row_MemberInside_GreysOutCheckInAndOffersCheckOut", async () => {
    mockApi(deskHandlers(rezaInside));
    renderSearch();

    const row = await rowOf("رضا احمدی");

    expect(within(row).getByText("داخل باشگاه")).toBeInTheDocument();
    expect(within(row).getByRole("button", { name: "ورود" })).toBeDisabled();
    expect(within(row).getByRole("button", { name: "خروج" })).toBeEnabled();
  });

  it("CheckIn_Pressed_AsksFirstAndSendsNothing", async () => {
    const api = mockApi(deskHandlers(reza));
    renderSearch();

    const dialog = await pressCheckIn();

    expect(dialog).toHaveTextContent("آیا از ثبت ورود رضا احمدی مطمئن هستید؟");
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
  });

  it("CheckIn_Cancelled_ClosesTheBoxAndSendsNothing", async () => {
    const api = mockApi(deskHandlers(reza));
    renderSearch();

    const dialog = await pressCheckIn();
    fireEvent.click(within(dialog).getByRole("button", { name: "انصراف" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
  });

  it("CheckIn_Confirmed_ShowsTheLockerPlanSessionsAndItemizedDebt", async () => {
    const api = mockApi(
      deskHandlers(reza, {
        [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
          json(201, { ...openVisit(reza.id), lockerNumber: 10, memberDebt: 2250000 }),
        [`GET /api/members/${reza.id}/debt`]: () =>
          memberDebt([
            debtItem({ outstanding: 2000000 }),
            serviceChargeDebtItem({ outstanding: 50000 }),
            debtItem({
              kind: "CafeOrder",
              id: "0199a000-0000-7000-8000-0000000000b3",
              planName: null,
              outstanding: 200000,
            }),
          ]),
      }),
    );
    renderSearch();

    const dialog = await confirmCheckIn();

    expect(await within(dialog).findByText("ورود ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByLabelText("کمد شماره ۱۰")).toHaveTextContent("۱۰");
    expect(await within(dialog).findByText("یک ماهه ۱۲ جلسه")).toBeInTheDocument();
    expect(within(dialog).getByText("۹ از ۱۲ جلسه")).toBeInTheDocument();

    const debt = await within(dialog).findByRole("region", { name: "بدهی" });
    expect(debt).toHaveTextContent("۲٬۲۵۰٬۰۰۰ تومان");
    expect(debt).toHaveTextContent("اشتراک ماهانه");
    expect(debt).toHaveTextContent("هوازی");
    expect(debt).toHaveTextContent("بوفه");
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(1);
  });

  it("CheckIn_Confirmed_TheResultStaysUntilTheDeskClosesIt", async () => {
    mockApi(
      deskHandlers(reza, {
        [`POST /api/members/${reza.id}/attendance/check-in`]: () => json(201, openVisit(reza.id)),
      }),
    );
    renderSearch();

    const dialog = await confirmCheckIn();
    await within(dialog).findByText("ورود ثبت شد");

    // A stray click on the page behind does not dismiss what the desk has to read.
    fireEvent.pointerDown(document.body);
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    const closeButtons = within(dialog).getAllByRole("button", { name: "بستن" });
    fireEvent.click(closeButtons[0]!);
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("CheckIn_NoFreeLocker_SaysSoWithoutFailing", async () => {
    mockApi(
      deskHandlers(reza, {
        [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
          json(201, openVisitNoLocker(reza.id)),
      }),
    );
    renderSearch();

    const dialog = await confirmCheckIn();

    expect(await within(dialog).findByText(/کمد آزادی نبود/)).toBeInTheDocument();
  });

  it("CheckIn_MemberWithNoDebt_SaysSo", async () => {
    mockApi(
      deskHandlers(reza, {
        [`POST /api/members/${reza.id}/attendance/check-in`]: () => json(201, openVisit(reza.id)),
      }),
    );
    renderSearch();

    const dialog = await confirmCheckIn();

    expect(await within(dialog).findByText("این عضو بدهی ندارد.")).toBeInTheDocument();
  });

  it("CheckIn_RefusedForAnotherReason_ShowsItWithoutOfferingASingleVisit", async () => {
    mockApi(
      deskHandlers(reza, {
        [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
          problem(422, "Attendance.AlreadyCheckedIn"),
      }),
    );
    renderSearch();

    const dialog = await confirmCheckIn();

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "این عضو هم‌اکنون داخل باشگاه است.",
    );
    expect(
      within(dialog).queryByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).not.toBeInTheDocument();
  });

  // ---- Single-session entry, inside the box (docs/ROADMAP.md 6.5.4) ----

  function refusedCheckIn(code = "Attendance.NoSubscription") {
    return deskHandlers(reza, {
      [`POST /api/members/${reza.id}/attendance/check-in`]: () => problem(422, code),
      "GET /api/plans": () => plansPage([singleSession]),
    });
  }

  it("CheckIn_MemberWithNoSubscription_OffersASingleVisitWithItsPrice", async () => {
    mockApi(refusedCheckIn());
    renderSearch();

    const dialog = await confirmCheckIn();

    expect(
      await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).toHaveTextContent("۱۵۰٬۰۰۰ تومان");
  });

  it("CheckIn_ExhaustedSubscription_AlsoOffersASingleVisit", async () => {
    // Not only "no subscription at all": a pack that ran out today is the same walk-in case.
    mockApi(refusedCheckIn("Subscriptions.NoSessionsLeft"));
    renderSearch();

    const dialog = await confirmCheckIn();

    expect(
      await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).toBeInTheDocument();
  });

  it("SingleVisit_Sold_SellsThenChecksInAndShowsTheLockerInTheSameBox", async () => {
    let checkedIn = false;
    const api = mockApi({
      ...refusedCheckIn(),
      "GET /api/members": () => membersPage([checkedIn ? rezaInside : reza]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        checkedIn
          ? json(201, { ...openVisit(reza.id), lockerNumber: 10 })
          : problem(422, "Attendance.NoSubscription"),
      [`POST /api/members/${reza.id}/subscriptions`]: () => {
        checkedIn = true;
        return json(201, { ...activeSubscription, isSingleSession: true });
      },
    });
    renderSearch();

    const dialog = await confirmCheckIn();
    fireEvent.click(await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }));

    // No second "are you sure": the priced button was the decision.
    expect(await within(dialog).findByText("ورود تک‌جلسه‌ای ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByLabelText("کمد شماره ۱۰")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${reza.id}/subscriptions`)).toHaveLength(1);
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(2);

    // Behind the box, the row has caught up: inside, so check-out is the live button.
    await waitFor(() => {
      const row = screen.getByRole("link", { name: "رضا احمدی", hidden: true }).closest("tr")!;
      expect(within(row).getByRole("button", { name: "خروج", hidden: true })).toBeEnabled();
    });
  });

  it("SingleVisit_NoSingleSessionPlanYet_SaysSoInsteadOfOfferingIt", async () => {
    mockApi({ ...refusedCheckIn(), "GET /api/plans": () => plansPage([]) });
    renderSearch();

    const dialog = await confirmCheckIn();

    expect(
      await within(dialog).findByText(/هنوز پلن تک‌جلسه‌ای ساخته نشده است/),
    ).toBeInTheDocument();
    expect(
      within(dialog).queryByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).not.toBeInTheDocument();
  });

  it("SingleVisit_SingleSessionPlanInactive_SaysSoInsteadOfOfferingIt", async () => {
    mockApi({
      ...refusedCheckIn(),
      "GET /api/plans": () => plansPage([{ ...singleSession, isActive: false }]),
    });
    renderSearch();

    const dialog = await confirmCheckIn();

    expect(await within(dialog).findByText(/پلن تک‌جلسه‌ای غیرفعال است/)).toBeInTheDocument();
  });

  // ---- Check-out ----

  async function pressCheckOut() {
    fireEvent.click(within(await rowOf("رضا احمدی")).getByRole("button", { name: "خروج" }));
    return screen.findByRole("dialog");
  }

  it("CheckOut_Pressed_AsksFirstWithTheLockerToTakeBackAndTheDebt", async () => {
    const api = mockApi(
      deskHandlers(rezaInside, {
        [`GET /api/members/${reza.id}/debt`]: () =>
          memberDebt([serviceChargeDebtItem({ outstanding: 50000 })]),
      }),
    );
    renderSearch();

    const dialog = await pressCheckOut();

    expect(dialog).toHaveTextContent("آیا از ثبت خروج رضا احمدی مطمئن هستید؟");
    expect(within(dialog).getByLabelText("کمد شماره ۱۰")).toBeInTheDocument();
    expect(within(dialog).getByText("کلید این کمد را تحویل بگیرید")).toBeInTheDocument();
    const debt = await within(dialog).findByRole("region", { name: "بدهی" });
    expect(debt).toHaveTextContent("هوازی");
    expect(debt).toHaveTextContent("۵۰٬۰۰۰ تومان");
    expect(api.requestsTo("POST", `/api/attendance/${visitId}/check-out`)).toHaveLength(0);
  });

  it("CheckOut_Confirmed_ChecksOutTheOpenVisit", async () => {
    const api = mockApi(
      deskHandlers(rezaInside, {
        [`POST /api/attendance/${visitId}/check-out`]: () => json(200, closedVisit(reza.id)),
      }),
    );
    renderSearch();

    const dialog = await pressCheckOut();
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" }));

    expect(await within(dialog).findByText("خروج ثبت شد")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/attendance/${visitId}/check-out`)).toHaveLength(1);
  });

  it("CheckOut_Cancelled_SendsNothing", async () => {
    const api = mockApi(deskHandlers(rezaInside));
    renderSearch();

    const dialog = await pressCheckOut();
    fireEvent.click(within(dialog).getByRole("button", { name: "انصراف" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("POST", `/api/attendance/${visitId}/check-out`)).toHaveLength(0);
  });

  // ---- Registering someone nobody found ----

  it("Search_NoResults_RegistersTheMemberThenAsksToCheckThemIn", async () => {
    const created = { ...reza, fullName: "سارا محمدی" };
    const api = mockApi({
      ...refusedCheckIn(),
      "GET /api/members": () => membersPage([]),
      "POST /api/members": () => json(201, created),
    });
    renderApp("/?q=" + encodeURIComponent("سارا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /ثبت این شخص/ }));
    // The name searched for is already in the form; only the rest is typed.
    expect(screen.getByLabelText("نام و نام خانوادگی")).toHaveValue("سارا");
    fireEvent.change(screen.getByLabelText("نام و نام خانوادگی"), {
      target: { value: "سارا محمدی" },
    });
    fireEvent.change(screen.getByLabelText("شماره موبایل"), {
      target: { value: "09121110000" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ثبت و ادامه" }));

    // Registered, then the same box as any check-in: ask, and a refusal offers a single visit.
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("آیا از ثبت ورود سارا محمدی مطمئن هستید؟");
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، ورود ثبت شود" }));
    expect(
      await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/members")).toHaveLength(1);
  });

  it("Search_NoResultsForAPhone_PrefillsThePhoneNotTheName", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([]) });
    renderApp("/?q=" + encodeURIComponent("۰۹۱۲۱۱۱۰۰۰۰"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /ثبت این شخص/ }));

    expect(screen.getByLabelText("شماره موبایل")).toHaveValue("۰۹۱۲۱۱۱۰۰۰۰");
    expect(screen.getByLabelText("نام و نام خانوادگی")).toHaveValue("");
  });
});
