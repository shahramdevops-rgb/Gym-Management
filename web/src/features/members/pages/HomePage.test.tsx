import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { mockApi, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { ali, membersPage, queryOf, reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

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
});
