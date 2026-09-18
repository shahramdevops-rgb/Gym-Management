import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { queryOf } from "@/test/members";
import { monthly12, plansPage, unlimitedQuarter } from "@/test/plans";
import { renderApp } from "@/test/renderApp";

describe("PlansPage", () => {
  it("PlansPage_StaffUser_SeesNoAccessMessageAndNoRequest", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/plans", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/plans")).toHaveLength(0);
  });

  it("PlansPage_Owner_ListsPlansInPersian", async () => {
    mockApi({
      ...signedInHandlers(owner),
      "GET /api/plans": () => plansPage([monthly12, unlimitedQuarter]),
    });

    renderApp("/plans", { session: session() });

    const monthlyRow = (await screen.findByText(monthly12.name)).closest("tr")!;
    expect(within(monthlyRow).getByText("۳۰ روز")).toBeInTheDocument();
    expect(within(monthlyRow).getByText("۱۲ جلسه")).toBeInTheDocument();
    expect(within(monthlyRow).getByText("۹۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    expect(within(monthlyRow).getByText("فعال")).toBeInTheDocument();
    expect(
      within(monthlyRow).getByRole("link", { name: `ویرایش ${monthly12.name}` }),
    ).toHaveAttribute("href", `/plans/${monthly12.id}/edit`);

    const quarterRow = screen.getByText(unlimitedQuarter.name).closest("tr")!;
    expect(within(quarterRow).getByText("نامحدود")).toBeInTheDocument();
    expect(within(quarterRow).getByText("۲٬۵۰۰٬۰۰۰٫۵ تومان")).toBeInTheDocument();
    expect(within(quarterRow).getByText("غیرفعال")).toBeInTheDocument();
    expect(within(quarterRow).getByRole("button", { name: "فعال‌سازی" })).toBeInTheDocument();
  });

  it("PlansPage_NoPlans_SaysSo", async () => {
    mockApi({ ...signedInHandlers(owner), "GET /api/plans": () => plansPage([]) });

    renderApp("/plans", { session: session() });

    expect(await screen.findByText("هنوز هیچ پلنی تعریف نشده است.")).toBeInTheDocument();
  });

  it("PlansPage_InactiveFilter_AsksTheApiAndPutsItInTheUrl", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/plans": (request) =>
        plansPage(queryOf(request).get("IsActive") === "false" ? [unlimitedQuarter] : [monthly12]),
    });
    const { router } = renderApp("/plans", { session: session() });

    await screen.findByText(monthly12.name);
    fireEvent.click(screen.getByRole("button", { name: "غیرفعال" }));

    expect(await screen.findByText(unlimitedQuarter.name)).toBeInTheDocument();
    expect(router.state.location.search).toBe("?status=inactive");
    const requests = api.requestsTo("GET", "/api/plans");
    expect(queryOf(requests[0]!).get("IsActive")).toBeNull();
    expect(queryOf(requests.at(-1)!).get("IsActive")).toBe("false");
  });

  it("PlansPage_Deactivate_CallsTheApiAndExplainsTheEffect", async () => {
    let current = monthly12;
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/plans": () => plansPage([current]),
      [`POST /api/plans/${monthly12.id}/deactivate`]: () => {
        current = { ...monthly12, isActive: false, version: 4 };
        return json(200, current);
      },
    });
    renderApp("/plans", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "غیرفعال‌سازی" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "دیگر فروخته نمی‌شود. اشتراک‌های فعلی تغییری نمی‌کنند.",
    );
    expect(api.requestsTo("POST", `/api/plans/${monthly12.id}/deactivate`)).toHaveLength(1);
    // The list is refetched, so the row shows the new state.
    expect(await screen.findByRole("button", { name: "فعال‌سازی" })).toBeInTheDocument();
  });

  it("PlansPage_ActivateFails_ShowsThePersianReason", async () => {
    mockApi({
      ...signedInHandlers(owner),
      "GET /api/plans": () => plansPage([unlimitedQuarter]),
      [`POST /api/plans/${unlimitedQuarter.id}/activate`]: () => problem(404, "Plans.NotFound"),
    });
    renderApp("/plans", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "فعال‌سازی" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("پلن پیدا نشد."));
  });
});
