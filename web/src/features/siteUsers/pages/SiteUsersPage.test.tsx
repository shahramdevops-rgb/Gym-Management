import { fireEvent, screen, waitFor } from "@testing-library/react";

import type { SiteUser } from "@/features/siteUsers/api";
import { json, mockApi, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

const sara: SiteUser = {
  id: "0199a000-0000-7000-8000-0000000000b1",
  fullName: "سارا محمدی",
  phoneNumber: "+989121234567",
  birthDate: "1995-04-20",
  signedUpAt: "2026-11-01T10:00:00Z",
};

function page(items: SiteUser[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 50, totalCount });
}

describe("SiteUsersPage", () => {
  it("SiteUsersPage_AsStaff_ShowsWhatTheyEnteredOnTheSite", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/site-users": () => page([sara]) });
    renderApp("/site-users", { session: session() });

    expect(await screen.findByText("سارا محمدی")).toBeInTheDocument();
    expect(screen.getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toBeInTheDocument();
    expect(screen.getByText("۱۳۷۴/۰۱/۳۱")).toBeInTheDocument();
    // Read-only: nothing on the page leads to a member's profile.
    expect(screen.queryByRole("link", { name: "سارا محمدی" })).not.toBeInTheDocument();
  });

  it("SiteUsersPage_Search_SendsTheNormalizedTextAndKeepsItInTheUrl", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/site-users": () => page([sara]),
    });
    const { router } = renderApp("/site-users", { session: session() });
    await screen.findByText("سارا محمدی");

    const box = screen.getByLabelText("نام یا شماره موبایل");
    fireEvent.change(box, { target: { value: "۴۵۶۷" } });
    fireEvent.submit(screen.getByRole("search"));

    await waitFor(() =>
      expect(router.state.location.search).toBe(`?q=${encodeURIComponent("۴۵۶۷")}`),
    );
    await waitFor(() =>
      expect(
        api
          .requestsTo("GET", "/api/site-users")
          .some((request) => new URL(request.url).searchParams.get("Search") === "4567"),
      ).toBe(true),
    );
  });

  it("SiteUsersPage_Nobody_SaysSo", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/site-users": () => page([]) });
    renderApp("/site-users", { session: session() });

    expect(
      await screen.findByText("هنوز کسی در سایت ثبت‌نام نکرده است که عضو نشده باشد."),
    ).toBeInTheDocument();
  });
});
