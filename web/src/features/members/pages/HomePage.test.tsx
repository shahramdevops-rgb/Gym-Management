import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { attendanceHistoryPage, openVisit, openVisitNoLocker } from "@/test/attendance";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { ali, membersPage, queryOf, reza } from "@/test/members";
import { plansPage, singleSession } from "@/test/plans";
import { renderApp } from "@/test/renderApp";
import { activeSubscription } from "@/test/subscriptions";

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

  // ---- Single-session entry (docs/ROADMAP.md 6.5.4) ----

  /** Check-in refused for want of a subscription is the walk-in case, not an error to dismiss. */
  function refusedCheckIn(code = "Attendance.NoSubscription") {
    return {
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () => problem(422, code),
      "GET /api/plans": () => plansPage([singleSession]),
    };
  }

  it("CheckIn_MemberWithNoSubscription_OffersASingleVisitWithItsPrice", async () => {
    mockApi(refusedCheckIn());
    renderApp("/?q=" + encodeURIComponent("رضا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(
      await screen.findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).toHaveTextContent("۱۵۰٬۰۰۰ تومان");
  });

  it("CheckIn_MemberWhoCanComeIn_NeverOffersASingleVisit", async () => {
    // The guard that matters: nobody is charged for a visit they already paid for. The offer
    // exists only because the API refused, so a successful check-in cannot produce it.
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () => json(201, openVisit(reza.id)),
      "GET /api/plans": () => plansPage([singleSession]),
    });
    renderApp("/?q=" + encodeURIComponent("رضا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    await screen.findByRole("status");
    expect(screen.queryByRole("button", { name: /ورود تک‌جلسه‌ای/ })).not.toBeInTheDocument();
  });

  it("SingleVisit_Sold_SellsTheSubscriptionThenChecksIn", async () => {
    let checkedIn = false;
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        checkedIn ? json(201, openVisit(reza.id)) : problem(422, "Attendance.NoSubscription"),
      [`POST /api/members/${reza.id}/subscriptions`]: () => {
        checkedIn = true;
        return json(201, { ...activeSubscription, isSingleSession: true });
      },
      "GET /api/plans": () => plansPage([singleSession]),
    });
    renderApp("/?q=" + encodeURIComponent("رضا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));
    fireEvent.click(await screen.findByRole("button", { name: /ورود تک‌جلسه‌ای/ }));

    expect(await screen.findByRole("status")).toHaveTextContent("ورود تک‌جلسه‌ای ثبت شد");
    const sales = api.requestsTo("POST", `/api/members/${reza.id}/subscriptions`);
    expect(sales).toHaveLength(1);
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(2);
  });

  it("SingleVisit_NoSingleSessionPlanYet_SaysSoInsteadOfOfferingIt", async () => {
    mockApi({
      ...refusedCheckIn(),
      "GET /api/plans": () => plansPage([]),
    });
    renderApp("/?q=" + encodeURIComponent("رضا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByText(/هنوز پلن تک‌جلسه‌ای ساخته نشده است/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /ورود تک‌جلسه‌ای/ })).not.toBeInTheDocument();
  });

  it("SingleVisit_SingleSessionPlanInactive_SaysSoInsteadOfOfferingIt", async () => {
    mockApi({
      ...refusedCheckIn(),
      "GET /api/plans": () => plansPage([{ ...singleSession, isActive: false }]),
    });
    renderApp("/?q=" + encodeURIComponent("رضا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByText(/پلن تک‌جلسه‌ای غیرفعال است/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /ورود تک‌جلسه‌ای/ })).not.toBeInTheDocument();
  });

  it("CheckIn_ExhaustedSubscription_AlsoOffersASingleVisit", async () => {
    // Not only "no subscription at all": a pack that ran out today is the same walk-in case.
    mockApi(refusedCheckIn("Subscriptions.NoSessionsLeft"));
    renderApp("/?q=" + encodeURIComponent("رضا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("button", { name: /ورود تک‌جلسه‌ای/ })).toBeInTheDocument();
  });

  it("CheckIn_AlreadyInside_ShowsTheErrorWithoutOfferingASingleVisit", async () => {
    // A refusal that selling a visit would not fix stays an ordinary error message.
    mockApi({
      ...refusedCheckIn("Attendance.AlreadyCheckedIn"),
    });
    renderApp("/?q=" + encodeURIComponent("رضا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("رضا احمدی");
    expect(screen.queryByRole("button", { name: /ورود تک‌جلسه‌ای/ })).not.toBeInTheDocument();
  });

  it("Search_NoResults_RegistersTheMemberAndGoesStraightToCheckIn", async () => {
    const created = { ...reza, id: reza.id, fullName: "سارا محمدی" };
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([]),
      "POST /api/members": () => json(201, created),
      [`POST /api/members/${created.id}/attendance/check-in`]: () =>
        problem(422, "Attendance.NoSubscription"),
      "GET /api/plans": () => plansPage([singleSession]),
    });
    renderApp("/?q=" + encodeURIComponent("سارا"), { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: /ثبت این شخص/ }));
    fireEvent.change(screen.getByLabelText("نام و نام خانوادگی"), {
      target: { value: "سارا محمدی" },
    });
    fireEvent.change(screen.getByLabelText("شماره موبایل"), {
      target: { value: "09121110000" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ثبت و ادامه" }));

    // Registered, then asked to come in, and the refusal opened the single-visit offer — all
    // without leaving the search screen.
    expect(
      await screen.findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).toBeInTheDocument();
    await waitFor(() => {
      expect(api.requestsTo("POST", "/api/members")).toHaveLength(1);
    });
  });

  // ---- One-click check-in (docs/ROADMAP.md 5.6) ----

  it("CheckIn_FromSearchResults_ShowsTheAssignedLocker", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () => json(201, openVisit(reza.id)),
    });
    renderApp("/", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "رضا" } });
    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "رضا احمدی: ورود ثبت شد. کمد شماره ۳",
    );
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(1);
  });

  it("CheckIn_NoFreeLocker_ShowsTheWarningWithoutFailing", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        json(201, openVisitNoLocker(reza.id)),
    });
    renderApp("/", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "رضا" } });
    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("کمد آزادی نبود");
  });

  it("CheckIn_MemberWhoOwesMoney_RecordsTheVisitAndWarnsAboutTheDebt", async () => {
    // Money owed never blocks a check-in (BUSINESS_RULES.md §0, §7): the visit is recorded and
    // the amount is something for the front desk to mention while the member is still there.
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        json(201, { ...openVisit(reza.id), memberDebt: 600000 }),
    });
    renderApp("/", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "رضا" } });
    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    const notice = await screen.findByRole("status");
    expect(notice).toHaveTextContent("ورود ثبت شد");
    expect(notice).toHaveTextContent("بدهی این عضو ۶۰۰٬۰۰۰ تومان است");
  });

  it("CheckIn_AlreadyCheckedIn_ShowsThePersianReason", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([reza]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        problem(422, "Attendance.AlreadyCheckedIn"),
    });
    renderApp("/", { session: session() });

    fireEvent.change(searchBox(), { target: { value: "رضا" } });
    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "رضا احمدی: این عضو هم‌اکنون داخل باشگاه است.",
    );
  });
});
