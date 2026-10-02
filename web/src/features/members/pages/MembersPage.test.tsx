import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { Member } from "@/features/members/api";
import { attendanceHistoryPage, closedVisit, openVisit } from "@/test/attendance";
import { json, mockApi, session, signedInHandlers, staffUser, type Handler } from "@/test/mockApi";
import { ali, memberDebt, membersPage, queryOf, reza, serviceChargeDebtItem } from "@/test/members";
import { renderApp } from "@/test/renderApp";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";

function searchBox() {
  return screen.getByRole("searchbox", { name: "نام یا شماره موبایل" });
}

/** The list requests that carried a search, leaving out the plain list the page opens with. */
function searchRequests(api: ReturnType<typeof mockApi>) {
  return api.requestsTo("GET", "/api/members").filter((request) => queryOf(request).has("Search"));
}

describe("MembersPage", () => {
  it("MembersPage_Default_ListsActiveAndInactiveMembersWithTheirStatus", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([ali, reza]),
    });

    renderApp("/members", { session: session() });

    const aliRow = (await screen.findByRole("link", { name: "علی رضایی" })).closest("tr")!;
    expect(within(aliRow).getByText("غیرفعال")).toBeInTheDocument();
    const rezaRow = screen.getByRole("link", { name: "رضا احمدی" }).closest("tr")!;
    expect(within(rezaRow).getByText("فعال")).toBeInTheDocument();
    expect(queryOf(api.requestsTo("GET", "/api/members")[0]!).has("IsActive")).toBe(false);
  });

  it("MembersPage_MemberWhoOwesMoney_ShowsHowMuchNotJustThatTheyOwe", async () => {
    // The gym runs open accounts (BUSINESS_RULES.md §5), so the amount is the useful part.
    const debtor = { ...reza, debt: 600000 };
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([debtor, ali]),
    });

    renderApp("/members", { session: session() });

    const debtorRow = (await screen.findByRole("link", { name: "رضا احمدی" })).closest("tr")!;
    expect(within(debtorRow).getByText("۶۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    const aliRow = screen.getByRole("link", { name: "علی رضایی" }).closest("tr")!;
    expect(within(aliRow).queryByText(/تومان/)).not.toBeInTheDocument();
  });

  it("MembersPage_FrozenMember_ShowsFrozenNextToTheStatusOnlyOnTheirRow", async () => {
    // BUSINESS_RULES.md §4 Freeze: a member whose subscription is frozen right now.
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([{ ...reza, isFrozen: true }, ali]),
    });

    renderApp("/members", { session: session() });

    const frozenRow = (await screen.findByRole("link", { name: "رضا احمدی" })).closest("tr")!;
    expect(within(frozenRow).getByText("فعال")).toBeInTheDocument();
    expect(within(frozenRow).getByText("فریز")).toBeInTheDocument();
    const aliRow = screen.getByRole("link", { name: "علی رضایی" }).closest("tr")!;
    expect(within(aliRow).queryByText("فریز")).not.toBeInTheDocument();
  });

  it("MembersPage_InactiveFilter_AsksOnlyForInactiveMembers", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": (request) =>
        membersPage(queryOf(request).get("IsActive") === "false" ? [ali] : [ali, reza]),
    });
    const { router } = renderApp("/members", { session: session() });
    await screen.findByRole("link", { name: "رضا احمدی" });

    fireEvent.click(screen.getByRole("button", { name: "غیرفعال" }));

    await waitFor(() =>
      expect(screen.queryByRole("link", { name: "رضا احمدی" })).not.toBeInTheDocument(),
    );
    expect(screen.getByRole("button", { name: "غیرفعال" })).toHaveAttribute("aria-pressed", "true");
    expect(router.state.location.search).toBe("?status=inactive");
    const last = api.requestsTo("GET", "/api/members").at(-1)!;
    expect(queryOf(last).get("IsActive")).toBe("false");
  });

  it("MembersPage_DebtorsFilter_AsksOnlyForDebtorsAndKeepsItInTheUrl", async () => {
    // Roadmap 6.5.20: the API filters before paging, so the page only has to ask.
    const debtor = { ...reza, debt: 600000 };
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": (request) =>
        membersPage(queryOf(request).get("DebtorsOnly") === "true" ? [debtor] : [ali, debtor]),
    });
    const { router } = renderApp("/members", { session: session() });
    await screen.findByRole("link", { name: "علی رضایی" });
    expect(queryOf(api.requestsTo("GET", "/api/members")[0]!).has("DebtorsOnly")).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "بدهکار" }));

    await waitFor(() =>
      expect(screen.queryByRole("link", { name: "علی رضایی" })).not.toBeInTheDocument(),
    );
    expect(screen.getByRole("button", { name: "بدهکار" })).toHaveAttribute("aria-pressed", "true");
    expect(router.state.location.search).toBe("?debt=1");
    expect(queryOf(api.requestsTo("GET", "/api/members").at(-1)!).get("DebtorsOnly")).toBe("true");
  });

  it("MembersPage_DebtorsWithAStatus_SendsBothAndPressingAgainClearsOnlyTheDebtors", async () => {
    // A toggle beside the status, not a fourth status: inactive members who still owe.
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([ali]),
    });
    const { router } = renderApp("/members?status=inactive&debt=1", { session: session() });
    await screen.findByRole("link", { name: "علی رضایی" });

    const first = queryOf(api.requestsTo("GET", "/api/members")[0]!);
    expect(first.get("IsActive")).toBe("false");
    expect(first.get("DebtorsOnly")).toBe("true");

    fireEvent.click(screen.getByRole("button", { name: "بدهکار" }));

    await waitFor(() => expect(router.state.location.search).toBe("?status=inactive"));
    expect(screen.getByRole("button", { name: "غیرفعال" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "بدهکار" })).toHaveAttribute("aria-pressed", "false");
    const last = queryOf(api.requestsTo("GET", "/api/members").at(-1)!);
    expect(last.has("DebtorsOnly")).toBe(false);
    expect(last.get("IsActive")).toBe("false");
  });

  it("MembersPage_NoDebtors_SaysNobodyOwes", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([]) });

    renderApp("/members?debt=1", { session: session() });

    expect(await screen.findByText("عضو بدهکاری نیست.")).toBeInTheDocument();
  });

  it("MembersPage_ManyMembers_ShowsPersianPageNumbersAndCount", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([reza], 45) });

    renderApp("/members", { session: session() });

    expect(await screen.findByText("صفحهٔ ۱ از ۳")).toBeInTheDocument();
    expect(screen.getByText("(۴۵)")).toBeInTheDocument();
  });

  it("MembersPage_PageInTheUrl_RequestsThatPage", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza], 45),
    });

    renderApp("/members?page=3", { session: session() });

    expect(await screen.findByText("صفحهٔ ۳ از ۳")).toBeInTheDocument();
    expect(queryOf(api.requestsTo("GET", "/api/members")[0]!).get("Page")).toBe("3");
  });

  it("MembersPage_NoMembersYet_SaysSo", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([]) });

    renderApp("/members", { session: session() });

    expect(await screen.findByText("هنوز هیچ عضوی ثبت نشده است.")).toBeInTheDocument();
  });

  // ---- The search box (it replaced the member search screen) ----

  it("Search_TypedWithArabicYe_SendsOneNormalizedRequestAfterThePause", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": (request) => membersPage(queryOf(request).has("Search") ? [ali] : []),
    });
    renderApp("/members", { session: session() });

    // Three quick key presses, the last with the Arabic ye (U+064A).
    fireEvent.change(searchBox(), { target: { value: "عل" } });
    fireEvent.change(searchBox(), { target: { value: "عل\u064A" } });
    fireEvent.change(searchBox(), { target: { value: "عل\u064A " } });

    expect(await screen.findByRole("link", { name: "علی رضایی" })).toBeInTheDocument();
    const requests = searchRequests(api);
    expect(requests).toHaveLength(1);
    expect(queryOf(requests[0]!).get("Search")).toBe("علی");
  });

  it("Search_PhoneWithPersianDigits_SendsEnglishDigits", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
    });
    renderApp("/members", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "۰۹۱۲ ۱۲۳ ۴۵۶۷" } });

    await waitFor(() => expect(searchRequests(api)).toHaveLength(1));
    expect(queryOf(searchRequests(api)[0]!).get("Search")).toBe("0912 123 4567");
  });

  it("Search_EnterKey_SearchesWithoutWaiting", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([reza]) });
    const { router } = renderApp("/members", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "رضا" } });
    fireEvent.submit(screen.getByRole("search"));

    // Straight into the URL, not after the debounce.
    expect(router.state.location.search).toBe(`?q=${encodeURIComponent("رضا")}`);
    expect(await screen.findByText("نتیجه جستجو")).toBeInTheDocument();
  });

  it("Search_OneCharacter_ShowsAHintAndKeepsTheWholeList", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([ali, reza]),
    });
    renderApp("/members", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "ع" } });

    expect(await screen.findByText("برای جستجو دست‌کم ۲ حرف وارد کنید.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "رضا احمدی" })).toBeInTheDocument();
    expect(searchRequests(api)).toHaveLength(0);
  });

  it("Search_InTheUrl_IsRestoredAfterAReload", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
    });

    // A reload, or "back" from a profile, arrives with the search already in the URL.
    renderApp(`/members?q=${encodeURIComponent("رضا")}`, { session: session() });

    expect(await screen.findByRole("link", { name: "رضا احمدی" })).toBeInTheDocument();
    expect(searchBox()).toHaveValue("رضا");
    expect(queryOf(api.requestsTo("GET", "/api/members")[0]!).get("Search")).toBe("رضا");
  });

  it("Search_WithAStatusFilter_SendsBothAndKeepsTheSearchWhenTheFilterChanges", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
    });
    const { router } = renderApp(`/members?q=${encodeURIComponent("رضا")}&status=active`, {
      session: session(),
    });

    await screen.findByRole("link", { name: "رضا احمدی" });
    const first = queryOf(api.requestsTo("GET", "/api/members")[0]!);
    expect(first.get("Search")).toBe("رضا");
    expect(first.get("IsActive")).toBe("true");

    fireEvent.click(screen.getByRole("button", { name: "همه" }));

    await waitFor(() =>
      expect(router.state.location.search).toBe(`?q=${encodeURIComponent("رضا")}`),
    );
    const last = queryOf(api.requestsTo("GET", "/api/members").at(-1)!);
    expect(last.get("Search")).toBe("رضا");
    expect(last.has("IsActive")).toBe(false);
  });

  it("Search_ManyResults_PagesKeepTheSearch", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza], 45),
    });
    renderApp(`/members?q=${encodeURIComponent("رضا")}`, { session: session() });

    expect(await screen.findByText("صفحهٔ ۱ از ۳")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "بعدی" }));

    await waitFor(() => expect(api.requestsTo("GET", "/api/members")).toHaveLength(2));
    const second = queryOf(api.requestsTo("GET", "/api/members")[1]!);
    expect(second.get("Page")).toBe("2");
    expect(second.get("Search")).toBe("رضا");
  });

  it("Search_OldSearchScreenLink_LandsOnTheMemberList", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([reza]) });
    const { router } = renderApp("/search", { session: session() });

    await waitFor(() => expect(router.state.location.pathname).toBe("/members"));
    expect(await screen.findByRole("link", { name: "رضا احمدی" })).toBeInTheDocument();
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
    const { router } = renderApp(`/members?q=${encodeURIComponent("سارا")}`, {
      session: session(),
    });

    expect(await screen.findByText("عضوی با این مشخصات پیدا نشد.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /ثبت این شخص/ }));
    // The name searched for is already in the form; only the rest is typed.
    expect(screen.getByLabelText("نام و نام خانوادگی")).toHaveValue("سارا");
    fireEvent.change(screen.getByLabelText("نام و نام خانوادگی"), {
      target: { value: "سارا محمدی" },
    });
    fireEvent.change(screen.getByLabelText("شماره موبایل"), {
      target: { value: "09121110000" },
    });
    fireEvent.change(screen.getByLabelText("تاریخ تولد"), { target: { value: "۱۳۷۰/۰۵/۱۲" } });
    fireEvent.click(screen.getByRole("button", { name: "ثبت و ادامه" }));

    // Registered, then the profile, where a plan is sold. Letting them in is the map's job.
    await waitFor(() => expect(router.state.location.pathname).toBe(`/members/${reza.id}`));
    expect(api.requestsTo("POST", "/api/members")).toHaveLength(1);
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
  });

  it("Search_NoResultsForAPhone_PrefillsThePhoneNotTheName", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([]) });
    renderApp(`/members?q=${encodeURIComponent("۰۹۱۲۱۱۱۰۰۰۰")}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /ثبت این شخص/ }));

    expect(screen.getByLabelText("شماره موبایل")).toHaveValue("۰۹۱۲۱۱۱۰۰۰۰");
    expect(screen.getByLabelText("نام و نام خانوادگی")).toHaveValue("");
  });

  it("MembersPage_NoSearch_OffersNoRegistering", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => membersPage([]) });
    renderApp("/members", { session: session() });

    expect(await screen.findByText("هنوز هیچ عضوی ثبت نشده است.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /ثبت این شخص/ })).not.toBeInTheDocument();
  });

  // ---- Someone inside: check-out from the row (BUSINESS_RULES.md §7 Confirming at the front desk) ----

  const visitId = openVisit(reza.id).id;

  /** Reza as the list sees him while he is inside, in locker ۱۰. */
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
  function deskHandlers(extra: Record<string, Handler> = {}) {
    return {
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([rezaInside]),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt([]),
      ...extra,
    };
  }

  async function rezaRow() {
    return (await screen.findByRole("link", { name: "رضا احمدی" })).closest("tr")!;
  }

  it("Row_MemberInside_ShowsTheLockerAndOffersCheckOutButNoCheckIn", async () => {
    mockApi(deskHandlers());
    renderApp("/members", { session: session() });

    const row = await rezaRow();

    expect(within(row).getByText("داخل باشگاه — کمد ۱۰")).toBeInTheDocument();
    expect(within(row).getByRole("button", { name: "خروج" })).toBeEnabled();
    // Check-in happens only on the locker map, where the locker is chosen.
    expect(within(row).queryByRole("button", { name: "ورود" })).not.toBeInTheDocument();
  });

  it("CheckOut_Confirmed_ChecksOutAndShowsTheDebtAgain", async () => {
    const api = mockApi(
      deskHandlers({
        [`POST /api/attendance/${visitId}/check-out`]: () => json(200, closedVisit(reza.id)),
        [`GET /api/members/${reza.id}/debt`]: () =>
          memberDebt([serviceChargeDebtItem({ outstanding: 50000 })]),
      }),
    );
    renderApp("/members", { session: session() });

    fireEvent.click(within(await rezaRow()).getByRole("button", { name: "خروج" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("آیا از ثبت خروج رضا احمدی مطمئن هستید؟");
    fireEvent.click(within(dialog).getByLabelText("کلید کمد شماره ۱۰ را تحویل گرفتم"));
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، خروج ثبت شود" }));

    expect(await within(dialog).findByText("خروج ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByText("کمد شماره ۱۰ آزاد شد.")).toBeInTheDocument();
    expect(await within(dialog).findByRole("region", { name: "بدهی" })).toHaveTextContent("هوازی");
    expect(api.requestsTo("POST", `/api/attendance/${visitId}/check-out`)).toHaveLength(1);
  });

  it("CheckOut_Cancelled_SendsNothing", async () => {
    const api = mockApi(deskHandlers());
    renderApp("/members", { session: session() });

    fireEvent.click(within(await rezaRow()).getByRole("button", { name: "خروج" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "انصراف" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("POST", `/api/attendance/${visitId}/check-out`)).toHaveLength(0);
  });
});
