import { fireEvent, screen, within } from "@testing-library/react";

import { sessionStore } from "@/features/auth/session";
import { mockApi, owner, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

describe("AppShell", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("AppShell_Rendered_ShowsPersianHeaderAndNavigation", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    expect(screen.getByRole("heading", { name: "مدیریت باشگاه" })).toBeInTheDocument();

    const navigation = screen.getByRole("navigation", { name: "منوی اصلی" });
    expect(within(navigation).getByRole("link", { name: "ورود با کمد" })).toHaveAttribute(
      "href",
      "/",
    );
    expect(within(navigation).getByRole("link", { name: "وضعیت سیستم" })).toHaveAttribute(
      "href",
      "/status",
    );
    expect(await screen.findByText(staffUser.fullName)).toBeInTheDocument();
  });

  it("AppShell_Staff_TheLockerMapIsTheFirstItemAndTheSearchIsOnTheMemberList", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    // BUSINESS_RULES.md §7: check-in happens only on the map, so it leads the menu for both roles.
    await screen.findByText(staffUser.fullName);
    const links = within(screen.getByRole("navigation", { name: "منوی اصلی" })).getAllByRole(
      "link",
    );
    expect(links[0]).toHaveTextContent("ورود با کمد");
    // The member search screen was folded into «اعضا» (the box sits above the list).
    expect(screen.queryByRole("link", { name: "جستجوی عضو" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "اعضا" })).toHaveAttribute("href", "/members");
    expect(screen.queryByRole("link", { name: "کمدها" })).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "ورود با کمد" })).toBeInTheDocument();
  });

  it("AppShell_CurrentRoute_MarksItsLinkActive", () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    expect(screen.getByRole("link", { name: "ورود با کمد" })).toHaveAttribute(
      "aria-current",
      "page",
    );
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
    expect(screen.getByRole("link", { name: "ورود با کمد" })).not.toHaveAttribute("aria-current");
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

  it("AppShell_Owner_SeesTheSettingsMenuItem", async () => {
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    expect(await screen.findByRole("link", { name: "تنظیمات" })).toHaveAttribute(
      "href",
      "/settings",
    );
  });

  it("AppShell_Anyone_SeesNoPlansMenuItem", async () => {
    // BUSINESS_RULES.md §3 since task 6.5.6: there is no list of plans to set up.
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    await screen.findByText(owner.fullName);
    expect(screen.queryByRole("link", { name: "پلن‌ها" })).not.toBeInTheDocument();
  });

  it("AppShell_Staff_DoesNotSeeTheSettingsMenuItem", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    // Changing the prices is the Owner's job (docs/BUSINESS_RULES.md §1); staff see them when selling.
    await screen.findByText(staffUser.fullName);
    expect(screen.queryByRole("link", { name: "تنظیمات" })).not.toBeInTheDocument();
  });

  it("AppShell_Owner_SeesTheExpensesMenuItem", async () => {
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    expect(await screen.findByRole("link", { name: "هزینه‌ها" })).toHaveAttribute(
      "href",
      "/expenses",
    );
  });

  it("AppShell_Staff_DoesNotSeeTheExpensesMenuItem", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    // Expenses are the Owner's, reading included (docs/BUSINESS_RULES.md §1, §9).
    await screen.findByText(staffUser.fullName);
    expect(screen.queryByRole("link", { name: "هزینه‌ها" })).not.toBeInTheDocument();
  });

  it("AppShell_Owner_SeesTheChequeAndInstallmentMenuItem", async () => {
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    expect(await screen.findByRole("link", { name: "چک و قسط" })).toHaveAttribute(
      "href",
      "/payables",
    );
  });

  it("AppShell_Staff_DoesNotSeeTheChequeAndInstallmentMenuItem", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    // Cheques and instalments are the Owner's, reading included (docs/BUSINESS_RULES.md §1, §9).
    await screen.findByText(staffUser.fullName);
    expect(screen.queryByRole("link", { name: "چک و قسط" })).not.toBeInTheDocument();
  });

  // ---- The pages released on 1405/07/18, marked for the Owner and the desk ----

  it("AppShell_On25Mehr_MarksMembersUpdatedAuditNewAndSmsInTesting", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-17T17:00:00Z")); // ۲۵ مهر ۱۴۰۵, 20:30 at the gym
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    const audit = await screen.findByRole("link", { name: "گزارش تغییرات" });
    expect(within(audit).getByText("جدید")).toBeInTheDocument();
    expect(
      within(screen.getByRole("link", { name: "اعضا" })).getByText("به‌روز شد"),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole("link", { name: "پیامک‌ها" })).getByText("آزمایشی"),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole("link", { name: "تنظیمات پیامک" })).getByText("آزمایشی"),
    ).toBeInTheDocument();
    expect(within(screen.getByRole("link", { name: "داشبورد" })).queryByText("جدید")).toBeNull();
  });

  it("AppShell_From26Mehr_KeepsOnlyTheSmsMarks", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-17T21:00:00Z")); // ۲۶ مهر ۱۴۰۵, 00:30 at the gym
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    await screen.findByRole("link", { name: "گزارش تغییرات" });
    expect(screen.queryByText("به‌روز شد")).not.toBeInTheDocument();
    expect(screen.queryByText("جدید")).not.toBeInTheDocument();
    // Until SMS goes live (task 10.6), not until a date.
    expect(screen.getAllByText("آزمایشی")).toHaveLength(2);
  });

  it("AppShell_Staff_SeesMembersUpdatedButNoOwnerMarks", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-10T17:00:00Z")); // ۱۸ مهر ۱۴۰۵
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    // The Owner's items wait for the user; once the name shows, the menu is complete.
    await screen.findByText(staffUser.fullName);
    expect(
      within(screen.getByRole("link", { name: "اعضا" })).getByText("به‌روز شد"),
    ).toBeInTheDocument();
    expect(screen.queryByText("آزمایشی")).not.toBeInTheDocument();
  });

  // ---- The theme (BUSINESS_RULES.md §14) ----

  it("AppShell_FirstVisit_OpensLightWithTheDarkButtonNotPressed", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    await screen.findByText(staffUser.fullName);
    expect(screen.getByRole("button", { name: "تم تیره" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
    expect(document.documentElement).not.toHaveClass("dark");
  });

  it("AppShell_DarkButton_TurnsTheWholeAppDarkAndBackAndRemembersIt", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });
    await screen.findByText(staffUser.fullName);
    const button = screen.getByRole("button", { name: "تم تیره" });

    fireEvent.click(button);

    // On <html>, so dialogs and the calendar (in portals outside the frame) go dark too.
    expect(document.documentElement).toHaveClass("dark");
    expect(button).toHaveAttribute("aria-pressed", "true");
    expect(localStorage.getItem("gym.theme")).toBe("dark");

    fireEvent.click(button);

    expect(document.documentElement).not.toHaveClass("dark");
    expect(button).toHaveAttribute("aria-pressed", "false");
    expect(localStorage.getItem("gym.theme")).toBe("light");
  });

  it("AppShell_OpenedDark_ShowsTheDarkButtonPressed", async () => {
    // What public/theme-init.js does on load when the device saved «تیره».
    document.documentElement.classList.add("dark");
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    await screen.findByText(staffUser.fullName);
    expect(screen.getByRole("button", { name: "تم تیره" })).toHaveAttribute("aria-pressed", "true");
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
