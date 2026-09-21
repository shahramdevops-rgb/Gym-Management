import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { mockApi, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { ali, membersPage, queryOf, reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

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

  it("MembersPage_MemberWithUnpaidSubscription_ShowsTheDebtBadge", async () => {
    const debtor = { ...reza, hasUnpaidSubscription: true };
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members": () => membersPage([debtor, ali]),
    });

    renderApp("/members", { session: session() });

    const debtorRow = (await screen.findByRole("link", { name: "رضا احمدی" })).closest("tr")!;
    expect(within(debtorRow).getByText("بدهکار")).toBeInTheDocument();
    const aliRow = screen.getByRole("link", { name: "علی رضایی" }).closest("tr")!;
    expect(within(aliRow).queryByText("بدهکار")).not.toBeInTheDocument();
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
});
