import { fireEvent, screen } from "@testing-library/react";

import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { ali, reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

describe("MemberProfilePage", () => {
  it("Profile_Loaded_ShowsNamePhoneNotesAndStatus", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("رضا احمدی")).toBeInTheDocument();
    expect(screen.getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toHaveAttribute("dir", "ltr");
    expect(screen.getByText("عضو قدیمی")).toBeInTheDocument();
    expect(screen.getByText("فعال")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ویرایش" })).toHaveAttribute(
      "href",
      `/members/${reza.id}/edit`,
    );
  });

  it("Profile_Deactivate_CallsTheApiAndShowsTheMemberAsInactive", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`POST /api/members/${reza.id}/deactivate`]: () =>
        json(200, { ...reza, isActive: false, version: 6 }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "غیرفعال‌سازی" }));

    expect(await screen.findByRole("status")).toHaveTextContent("عضو غیرفعال شد.");
    expect(screen.getByText("غیرفعال")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "فعال‌سازی" })).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${reza.id}/deactivate`)).toHaveLength(1);
  });

  it("Profile_Reactivate_CallsTheApiAndShowsTheMemberAsActive", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${ali.id}`]: () => json(200, ali),
      [`POST /api/members/${ali.id}/reactivate`]: () =>
        json(200, { ...ali, isActive: true, version: 8 }),
    });
    renderApp(`/members/${ali.id}`, { session: session() });

    expect(await screen.findByText(/نمی‌تواند وارد باشگاه شود/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "فعال‌سازی" }));

    expect(await screen.findByRole("status")).toHaveTextContent("عضو دوباره فعال شد.");
    expect(screen.getByText("فعال")).toBeInTheDocument();
    expect(screen.queryByText(/نمی‌تواند وارد باشگاه شود/)).not.toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${ali.id}/reactivate`)).toHaveLength(1);
  });

  it("Profile_DeactivateFails_ShowsThePersianError", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`POST /api/members/${reza.id}/deactivate`]: () =>
        problem(409, "Members.ChangedConcurrently"),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "غیرفعال‌سازی" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("هم‌زمان توسط شخص دیگری ویرایش شد");
  });

  it("Profile_UnknownId_SaysTheMemberWasNotFoundWithoutRetrying", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => problem(404, "Members.NotFound"),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("عضو پیدا نشد.")).toBeInTheDocument();
    expect(api.requestsTo("GET", `/api/members/${reza.id}`)).toHaveLength(1);
  });
});
