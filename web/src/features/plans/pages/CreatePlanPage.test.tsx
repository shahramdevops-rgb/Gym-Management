import { fireEvent, screen, waitFor } from "@testing-library/react";

import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { monthly12, plansPage } from "@/test/plans";
import { renderApp } from "@/test/renderApp";

interface Fill {
  name?: string;
  durationDays?: string;
  unlimited?: boolean;
  sessionCount?: string;
  price?: string;
}

async function fill({
  name = "",
  durationDays = "",
  unlimited = false,
  sessionCount = "",
  price = "",
}: Fill) {
  // The page is behind RequireRole, so it appears once the current user has loaded.
  fireEvent.change(await screen.findByLabelText("نام پلن"), { target: { value: name } });
  fireEvent.change(screen.getByLabelText("مدت (روز)"), { target: { value: durationDays } });
  fireEvent.change(screen.getByLabelText("تعداد جلسات"), { target: { value: sessionCount } });
  if (unlimited) {
    fireEvent.click(screen.getByLabelText("تعداد جلسات نامحدود"));
  }
  fireEvent.change(screen.getByLabelText("قیمت (تومان)"), { target: { value: price } });
  fireEvent.click(screen.getByRole("button", { name: "ثبت پلن" }));
}

async function sentBody(api: ReturnType<typeof mockApi>) {
  await waitFor(() => expect(api.requestsTo("POST", "/api/plans")).toHaveLength(1));
  return (await api.requestsTo("POST", "/api/plans")[0]!.json()) as Record<string, unknown>;
}

function ownerApi(extra: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    ...signedInHandlers(owner),
    "POST /api/plans": () => json(201, monthly12),
    "GET /api/plans": () => plansPage([monthly12]),
    ...extra,
  });
}

describe("CreatePlanPage", () => {
  it("CreatePlan_StaffUser_SeesNoAccessMessage", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/plans/new", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
  });

  it("CreatePlan_PersianInput_SendsNormalizedValuesAndOpensTheList", async () => {
    const api = ownerApi();
    const { router } = renderApp("/plans/new", { session: session() });

    // Arabic ye (U+064A), Persian digits, Persian thousands separators.
    await fill({
      name: " یک ماهه ۱۲ جلسهي ",
      durationDays: "۳۰",
      sessionCount: "۱۲",
      price: "۹۰۰٬۰۰۰",
    });

    expect(await sentBody(api)).toEqual({
      name: "یک ماهه ۱۲ جلسهی",
      durationDays: 30,
      sessionCount: 12,
      // A string, so no price is ever squeezed through a JavaScript number.
      price: "900000",
    });
    await waitFor(() => expect(router.state.location.pathname).toBe("/plans"));
  });

  it("CreatePlan_Unlimited_SendsNullSessionCount", async () => {
    const api = ownerApi();
    renderApp("/plans/new", { session: session() });

    await fill({ name: "ماهانه آزاد", durationDays: "30", unlimited: true, price: "1,500,000.50" });

    const body = await sentBody(api);
    expect(body.sessionCount).toBeNull();
    expect(body.price).toBe("1500000.50");
  });

  it("CreatePlan_UnlimitedTicked_DisablesTheSessionCount", async () => {
    ownerApi();
    renderApp("/plans/new", { session: session() });

    const sessions = await screen.findByLabelText("تعداد جلسات");
    expect(sessions).toBeEnabled();

    fireEvent.click(screen.getByLabelText("تعداد جلسات نامحدود"));

    expect(sessions).toBeDisabled();
  });

  it("CreatePlan_EmptyForm_ShowsEveryFieldErrorBeforeSending", async () => {
    const api = ownerApi();
    renderApp("/plans/new", { session: session() });

    await fill({});

    expect(await screen.findByText("نام پلن را وارد کنید.")).toBeInTheDocument();
    expect(screen.getByText("مدت پلن باید بین ۱ تا ۳۶۵ روز باشد.")).toBeInTheDocument();
    // Neither unlimited nor a count: shown together with the others, not after them.
    expect(
      screen.getByText("تعداد جلسات باید بین ۱ تا ۳۶۵ باشد، یا برای نامحدود خالی بماند."),
    ).toBeInTheDocument();
    expect(screen.getByText("قیمت را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/plans")).toHaveLength(0);
  });

  it.each([
    [{ durationDays: "0" }, "مدت پلن باید بین ۱ تا ۳۶۵ روز باشد."],
    [{ durationDays: "366" }, "مدت پلن باید بین ۱ تا ۳۶۵ روز باشد."],
    [{ sessionCount: "0" }, "تعداد جلسات باید بین ۱ تا ۳۶۵ باشد، یا برای نامحدود خالی بماند."],
    [{ price: "10.001" }, "قیمت حداکثر می‌تواند ۲ رقم اعشار داشته باشد."],
    [{ name: "ن".repeat(101) }, "نام پلن بیش از حد طولانی است."],
  ])("CreatePlan_InvalidField %j_IsRejectedBeforeSending", async (change, text) => {
    const api = ownerApi();
    renderApp("/plans/new", { session: session() });

    await fill({ name: "پلن", durationDays: "30", sessionCount: "12", price: "900000", ...change });

    expect(await screen.findByText(text)).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/plans")).toHaveLength(0);
  });

  it("CreatePlan_NameTaken_ShowsTheReasonUnderTheName", async () => {
    ownerApi({ "POST /api/plans": () => problem(409, "Plans.NameAlreadyExists") });
    const { router } = renderApp("/plans/new", { session: session() });

    await fill({ name: "یک ماهه", durationDays: "30", sessionCount: "12", price: "900000" });

    const message = await screen.findByText("پلن دیگری با همین نام وجود دارد.");
    expect(screen.getByLabelText("نام پلن")).toHaveAttribute("aria-describedby", message.id);
    expect(router.state.location.pathname).toBe("/plans/new");
  });

  it("CreatePlan_ServerFieldErrors_AppearUnderTheirFields", async () => {
    ownerApi({
      "POST /api/plans": () =>
        problem(400, "General.ValidationFailed", {
          errors: { price: [{ code: "Plans.PriceTooLarge", description: "too large" }] },
        }),
    });
    renderApp("/plans/new", { session: session() });

    await fill({ name: "پلن", durationDays: "30", sessionCount: "12", price: "900000" });

    const message = await screen.findByText("قیمت بیش از حد بزرگ است.");
    // The price box is described by its words line as well as by the error, so both ids are there.
    expect(screen.getByLabelText("قیمت (تومان)").getAttribute("aria-describedby")).toContain(
      message.id,
    );
  });

  it("CreatePlan_MinusTyped_NeverReachesThePriceBox", async () => {
    // A negative price used to be typed and then refused by the schema. The money field does not
    // let it be typed at all, which is why "-1" is no longer among the rejected values above.
    const api = ownerApi();
    renderApp("/plans/new", { session: session() });

    await fill({ name: "پلن", durationDays: "30", sessionCount: "12", price: "-900000" });

    expect(await sentBody(api)).toMatchObject({ price: "900000" });
  });

  it("CreatePlan_PriceTypedWithPersianDigits_IsSentAsPlainDigits", async () => {
    const api = ownerApi();
    renderApp("/plans/new", { session: session() });

    await fill({ name: "پلن", durationDays: "30", sessionCount: "12", price: "۱٬۵۰۰٬۰۰۰" });

    expect(screen.getByText("یک میلیون و پانصد هزار تومان")).toBeInTheDocument();
    expect(await sentBody(api)).toMatchObject({ price: "1500000" });
  });
});
