import { fireEvent, screen, waitFor } from "@testing-library/react";

import { json, mockApi, owner, problem, session, signedInHandlers } from "@/test/mockApi";
import { monthly12, plansPage, unlimitedQuarter } from "@/test/plans";
import { renderApp } from "@/test/renderApp";

const editPath = `/plans/${monthly12.id}/edit`;

describe("EditPlanPage", () => {
  it("EditPlan_Loaded_ShowsTheCurrentValues", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/plans/${monthly12.id}`]: () => json(200, monthly12),
    });

    renderApp(editPath, { session: session() });

    expect(await screen.findByLabelText("نام پلن")).toHaveValue(monthly12.name);
    expect(screen.getByLabelText("مدت (روز)")).toHaveValue("30");
    expect(screen.getByLabelText("تعداد جلسات نامحدود")).not.toBeChecked();
    expect(screen.getByLabelText("تعداد جلسات")).toHaveValue("12");
    expect(screen.getByLabelText("قیمت (تومان)")).toHaveValue("900000");
  });

  it("EditPlan_UnlimitedPlan_ShowsTheBoxTicked", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/plans/${unlimitedQuarter.id}`]: () => json(200, unlimitedQuarter),
    });

    renderApp(`/plans/${unlimitedQuarter.id}/edit`, { session: session() });

    expect(await screen.findByLabelText("تعداد جلسات نامحدود")).toBeChecked();
    expect(screen.getByLabelText("تعداد جلسات")).toBeDisabled();
    expect(screen.getByLabelText("قیمت (تومان)")).toHaveValue("2500000.5");
  });

  it("EditPlan_Saved_SendsTheVersionItWasFilledFromAndOpensTheList", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      [`GET /api/plans/${monthly12.id}`]: () => json(200, monthly12),
      [`PUT /api/plans/${monthly12.id}`]: () =>
        json(200, { ...monthly12, price: 950000, version: 4 }),
      "GET /api/plans": () => plansPage([monthly12]),
    });
    const { router } = renderApp(editPath, { session: session() });

    fireEvent.change(await screen.findByLabelText("قیمت (تومان)"), {
      target: { value: "۹۵۰٬۰۰۰" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/plans"));
    const body = (await api.requestsTo("PUT", `/api/plans/${monthly12.id}`)[0]!.json()) as Record<
      string,
      unknown
    >;
    expect(body).toEqual({
      name: monthly12.name,
      durationDays: 30,
      sessionCount: 12,
      price: "950000",
      version: 3,
    });
  });

  it("EditPlan_SomeoneSavedMeanwhile_ExplainsAndReloadsTheirVersion", async () => {
    const theirs = { ...monthly12, name: "یک ماهه ویژه", version: 4 };
    let current = monthly12;
    const api = mockApi({
      ...signedInHandlers(owner),
      [`GET /api/plans/${monthly12.id}`]: () => json(200, current),
      [`PUT /api/plans/${monthly12.id}`]: () => problem(409, "Plans.ChangedConcurrently"),
    });
    const { router } = renderApp(editPath, { session: session() });

    fireEvent.change(await screen.findByLabelText("نام پلن"), { target: { value: "نام من" } });
    // The other Owner session saves first.
    current = theirs;
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText(/هم‌زمان توسط شخص دیگری ویرایش شد/)).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(editPath);

    fireEvent.click(screen.getByRole("button", { name: "بارگذاری اطلاعات تازه" }));

    await waitFor(() => expect(screen.getByLabelText("نام پلن")).toHaveValue(theirs.name));
    expect(screen.queryByText(/هم‌زمان توسط شخص دیگری ویرایش شد/)).not.toBeInTheDocument();
    expect(api.requestsTo("GET", `/api/plans/${monthly12.id}`)).toHaveLength(2);
  });

  it("EditPlan_UnknownId_SaysThePlanWasNotFound", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET /api/plans/${monthly12.id}`]: () => problem(404, "Plans.NotFound"),
    });

    renderApp(editPath, { session: session() });

    expect(await screen.findByText("پلن پیدا نشد.")).toBeInTheDocument();
  });
});
