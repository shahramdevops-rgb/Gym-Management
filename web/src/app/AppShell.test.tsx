import { screen, within } from "@testing-library/react";

import { renderApp } from "@/test/renderApp";

describe("AppShell", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Healthy")));
  });

  it("AppShell_Rendered_ShowsPersianHeaderAndNavigation", () => {
    renderApp("/");

    expect(screen.getByRole("heading", { name: "مدیریت باشگاه" })).toBeInTheDocument();

    const navigation = screen.getByRole("navigation", { name: "منوی اصلی" });
    expect(within(navigation).getByRole("link", { name: "وضعیت سیستم" })).toHaveAttribute(
      "href",
      "/",
    );
  });

  it("AppShell_CurrentRoute_MarksItsLinkActive", () => {
    renderApp("/");

    expect(screen.getByRole("link", { name: "وضعیت سیستم" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("AppShell_NavigationBeforeContent_SoRtlPlacesItOnTheRight", () => {
    renderApp("/");

    // In a right-to-left row the first child sits on the right. The layout depends on this
    // order (and on logical classes), not on any "right" utility.
    const navigation = screen.getByRole("navigation");
    const main = screen.getByRole("main");
    expect(
      navigation.compareDocumentPosition(main) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(navigation).toHaveClass("border-e");
  });
});
