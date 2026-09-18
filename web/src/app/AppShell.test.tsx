import { fireEvent, screen, within } from "@testing-library/react";

import { sessionStore } from "@/features/auth/session";
import { mockApi, owner, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

describe("AppShell", () => {
  it("AppShell_Rendered_ShowsPersianHeaderAndNavigation", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    expect(screen.getByRole("heading", { name: "مدیریت باشگاه" })).toBeInTheDocument();

    const navigation = screen.getByRole("navigation", { name: "منوی اصلی" });
    expect(within(navigation).getByRole("link", { name: "جستجو" })).toHaveAttribute("href", "/");
    expect(within(navigation).getByRole("link", { name: "وضعیت سیستم" })).toHaveAttribute(
      "href",
      "/status",
    );
    expect(await screen.findByText(staffUser.fullName)).toBeInTheDocument();
  });

  it("AppShell_CurrentRoute_MarksItsLinkActive", () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    expect(screen.getByRole("link", { name: "جستجو" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "اعضا" })).not.toHaveAttribute("aria-current");
  });

  it("AppShell_MemberProfile_KeepsTheMembersLinkActive", () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/members/0199a000-0000-7000-8000-0000000000aa": () =>
        new Response(null, { status: 404 }),
    });

    renderApp("/members/0199a000-0000-7000-8000-0000000000aa", { session: session() });

    expect(screen.getByRole("link", { name: "اعضا" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "جستجو" })).not.toHaveAttribute("aria-current");
  });

  it("AppShell_Staff_SeesTheMemberMenuItems", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    // Members are front-desk work: both roles (docs/BUSINESS_RULES.md §1, permissions).
    await screen.findByText(staffUser.fullName);
    expect(screen.getByRole("link", { name: "اعضا" })).toHaveAttribute("href", "/members");
  });

  it("AppShell_NavigationBeforeContent_SoRtlPlacesItOnTheRight", () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    // In a right-to-left row the first child sits on the right. The layout depends on this
    // order (and on logical classes), not on any "right" utility.
    const navigation = screen.getByRole("navigation");
    const main = screen.getByRole("main");
    expect(
      navigation.compareDocumentPosition(main) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(navigation).toHaveClass("border-e");
  });

  it("AppShell_Owner_SeesTheStaffMenuItem", async () => {
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    expect(await screen.findByRole("link", { name: "کارمندان" })).toHaveAttribute("href", "/staff");
  });

  it("AppShell_Staff_DoesNotSeeTheStaffMenuItem", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    // Wait for /me, so the check is not passing merely because roles have not loaded yet.
    await screen.findByText(staffUser.fullName);
    expect(screen.queryByRole("link", { name: "کارمندان" })).not.toBeInTheDocument();
  });

  it("AppShell_Logout_RevokesTheSessionAndShowsTheLoginPage", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "POST /api/auth/logout": () => new Response(null, { status: 204 }),
    });

    renderApp("/", { session: session() });
    fireEvent.click(screen.getByRole("button", { name: "خروج" }));

    expect(await screen.findByRole("button", { name: "ورود" })).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/auth/logout")).toHaveLength(1);
    expect(sessionStore.getState().status).toBe("signedOut");
  });
});
