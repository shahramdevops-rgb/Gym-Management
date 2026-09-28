import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { Member } from "@/features/members/api";
import { attendanceHistoryPage, closedVisit, openVisit } from "@/test/attendance";
import { clickOutsideDialog } from "@/test/dialog";
import { json, mockApi, session, signedInHandlers, staffUser, type Handler } from "@/test/mockApi";
import { ali, memberDebt, membersPage, queryOf, reza, serviceChargeDebtItem } from "@/test/members";
import { renderApp } from "@/test/renderApp";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";

// Every test signs in as Staff: finding members is front-desk work (BUSINESS_RULES.md §1).
function searchBox() {
  return screen.getByRole("searchbox", { name: "نام یا شماره موبایل" });
}

describe("MemberSearchPage", () => {
  it("Search_TypedWithArabicYe_SendsOneNormalizedRequestAfterThePause", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([ali]),
    });
    renderApp("/search", { session: session() });

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
    renderApp("/search", { session: session() });

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
    const { router } = renderApp("/search", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "رضا" } });
    fireEvent.submit(screen.getByRole("search"));

    // Straight into the URL, not after the debounce.
    expect(router.state.location.search).toBe(`?q=${encodeURIComponent("رضا")}`);
    await screen.findByRole("link", { name: "رضا احمدی" });
    expect(api.requestsTo("GET", "/api/members")).toHaveLength(1);
  });

  it("Search_OneCharacter_SendsNothingAndShowsAHint", async () => {
    const api = mockApi(signedInHandlers(staffUser));
    renderApp("/search", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "ع" } });

    expect(await screen.findByText("برای جستجو دست‌کم ۲ حرف وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/members")).toHaveLength(0);
  });

  it("Search_NobodyMatches_SaysSo", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([]) });
    renderApp("/search", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "ناشناس" } });

    expect(await screen.findByText("عضوی با این مشخصات پیدا نشد.")).toBeInTheDocument();
  });

  it("Search_InTheUrl_IsRestoredAfterAReload", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
    });

    // A reload, or "back" from a profile, arrives with the search already in the URL.
    renderApp(`/search?q=${encodeURIComponent("رضا")}`, { session: session() });

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
    const { router } = renderApp(`/search?q=${encodeURIComponent("رضا")}`, { session: session() });

    fireEvent.click(await screen.findByRole("link", { name: "رضا احمدی" }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/members/${reza.id}`));
    expect(await screen.findByText("عضو قدیمی")).toBeInTheDocument();
  });

  it("Search_ManyResults_PagesWithPersianNumbers", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza], 45),
    });
    renderApp(`/search?q=${encodeURIComponent("رضا")}`, { session: session() });

    expect(await screen.findByText("صفحهٔ ۱ از ۳")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "بعدی" }));

    await waitFor(() => expect(api.requestsTo("GET", "/api/members")).toHaveLength(2));
    const second = queryOf(api.requestsTo("GET", "/api/members")[1]!);
    expect(second.get("Page")).toBe("2");
    expect(second.get("Search")).toBe("رضا");
  });

  // ---- Someone inside: the locker and check-out (BUSINESS_RULES.md §7 Confirming at the front desk) ----

  const visitId = openVisit(reza.id).id;

  /** Reza as the search sees him while he is inside, in locker ۱۰. */
  const rezaInside: Member = {
    ...reza,
    currentVisit: {
      attendanceId: visitId,
      lockerNumber: 10,
      usesReservePlace: false,
      checkedInAt: "2026-09-18T07:00:00Z",
    },
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
    return renderApp("/search?q=" + encodeURIComponent("رضا"), { session: session() });
  }

  async function rowOf(name: string) {
    return (await screen.findByRole("link", { name })).closest("tr")!;
  }

  it("Row_MemberOutside_OffersNoCheckIn", async () => {
    mockApi(deskHandlers(reza));
    renderSearch();

    const row = await rowOf("رضا احمدی");

    // Check-in happens only on the locker map since 6.5.5, where the locker is chosen.
    expect(within(row).queryByRole("button", { name: "ورود" })).not.toBeInTheDocument();
    expect(within(row).queryByRole("button", { name: "خروج" })).not.toBeInTheDocument();
    expect(within(row).queryByText(/داخل باشگاه/)).not.toBeInTheDocument();
  });

  it("Row_MemberInside_ShowsTheLockerAndOffersCheckOut", async () => {
    mockApi(deskHandlers(rezaInside));
    renderSearch();

    const row = await rowOf("رضا احمدی");

    expect(within(row).getByText("داخل باشگاه — کمد ۱۰")).toBeInTheDocument();
    expect(within(row).getByRole("button", { name: "خروج" })).toBeEnabled();
    expect(within(row).queryByRole("button", { name: "ورود" })).not.toBeInTheDocument();
  });

  it("Row_MemberOnAReservePlace_SaysSoInsteadOfALocker", async () => {
    mockApi(
      deskHandlers({
        ...rezaInside,
        currentVisit: { ...rezaInside.currentVisit!, lockerNumber: null, usesReservePlace: true },
      }),
    );
    renderSearch();

    expect(
      within(await rowOf("رضا احمدی")).getByText("داخل باشگاه — بدون کمد"),
    ).toBeInTheDocument();
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
    expect(within(dialog).getByText("کلید کمد را از عضو تحویل بگیرید")).toBeInTheDocument();
    const debt = await within(dialog).findByRole("region", { name: "بدهی" });
    expect(debt).toHaveTextContent("هوازی");
    expect(debt).toHaveTextContent("۵۰٬۰۰۰ تومان");
    expect(api.requestsTo("POST", `/api/attendance/${visitId}/check-out`)).toHaveLength(0);
  });

  it("CheckOut_KeyNotTickedYet_CannotBeConfirmed", async () => {
    mockApi(deskHandlers(rezaInside));
    renderSearch();

    const dialog = await pressCheckOut();
    const confirm = within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" });

    // The locker goes to the next person in once the visit closes, so the key comes back first.
    expect(confirm).toBeDisabled();
    fireEvent.click(within(dialog).getByLabelText("کلید کمد شماره ۱۰ را تحویل گرفتم"));
    expect(confirm).toBeEnabled();
  });

  it("CheckOut_NoLocker_NeedsNoKeyTick", async () => {
    mockApi(
      deskHandlers({
        ...rezaInside,
        currentVisit: { ...rezaInside.currentVisit!, lockerNumber: null },
      }),
    );
    renderSearch();

    const dialog = await pressCheckOut();

    expect(within(dialog).queryByText("کلید کمد را از عضو تحویل بگیرید")).not.toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" })).toBeEnabled();
  });

  it("CheckOut_Confirmed_ChecksOutAndShowsTheDebtAgain", async () => {
    const api = mockApi(
      deskHandlers(rezaInside, {
        [`POST /api/attendance/${visitId}/check-out`]: () => json(200, closedVisit(reza.id)),
        [`GET /api/members/${reza.id}/debt`]: () =>
          memberDebt([serviceChargeDebtItem({ outstanding: 50000 })]),
      }),
    );
    renderSearch();

    const dialog = await pressCheckOut();
    fireEvent.click(within(dialog).getByLabelText("کلید کمد شماره ۱۰ را تحویل گرفتم"));
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" }));

    expect(await within(dialog).findByText("خروج ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByText("کمد شماره ۱۰ آزاد شد.")).toBeInTheDocument();
    // The last moment to collect: the itemized debt is still in front of the desk.
    expect(await within(dialog).findByRole("region", { name: "بدهی" })).toHaveTextContent("هوازی");
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

  it("CheckOut_ClickedOutside_ClosesAndSendsNothing", async () => {
    const api = mockApi(deskHandlers(rezaInside));
    renderSearch();

    await pressCheckOut();
    await clickOutsideDialog();

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("POST", `/api/attendance/${visitId}/check-out`)).toHaveLength(0);
  });

  // ---- Registering someone nobody found ----

  it("Search_NoResults_RegistersTheMemberThenOpensTheirProfile", async () => {
    const created = { ...reza, fullName: "سارا محمدی" };
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([]),
      "POST /api/members": () => json(201, created),
      [`GET /api/members/${reza.id}`]: () => json(200, created),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
    });
    const { router } = renderApp("/search?q=" + encodeURIComponent("سارا"), { session: session() });

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

    // Registered, then the profile, where a plan is sold. Letting them in is the map's job.
    await waitFor(() => expect(router.state.location.pathname).toBe(`/members/${reza.id}`));
    expect(api.requestsTo("POST", "/api/members")).toHaveLength(1);
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
  });

  it("Search_NoResultsForAPhone_PrefillsThePhoneNotTheName", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([]) });
    renderApp("/search?q=" + encodeURIComponent("۰۹۱۲۱۱۱۰۰۰۰"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /ثبت این شخص/ }));

    expect(screen.getByLabelText("شماره موبایل")).toHaveValue("۰۹۱۲۱۱۱۰۰۰۰");
    expect(screen.getByLabelText("نام و نام خانوادگی")).toHaveValue("");
  });
});
