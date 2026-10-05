import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import { json, mockApi, owner, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { ali, needsAttention, reportHandlers } from "@/test/reports";
import { renderApp } from "@/test/renderApp";

import { presetRange } from "../range";

const dashboard = { ...signedInHandlers(owner), ...reportHandlers };

/** The reports that take a range; the other three are of today. */
const rangeReports = [
  "/api/reports/financial",
  "/api/reports/attendance",
  "/api/reports/members",
  "/api/reports/cafe-products",
];

function queryOf(request: Request) {
  const url = new URL(request.url);
  return { from: url.searchParams.get("From"), to: url.searchParams.get("To") };
}

function card(label: string) {
  const term = screen.getAllByText(label).find((element) => element.tagName === "DT");
  if (term === undefined || term.parentElement === null) {
    throw new Error(`No card is labelled ${label}`);
  }
  return term.parentElement;
}

describe("DashboardPage", () => {
  it("DashboardPage_Staff_SeesNoAccessMessageAndNoRequest", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/dashboard", { session: session() });

    // §12: reports are the Owner's, reading included.
    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/reports/financial")).toHaveLength(0);
    expect(api.requestsTo("GET", "/api/reports/needs-attention")).toHaveLength(0);
  });

  it("DashboardPage_Staff_HasNoMenuItem", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    await screen.findByRole("link", { name: /ورود با کمد/ });
    expect(screen.queryByRole("link", { name: /داشبورد/ })).not.toBeInTheDocument();
  });

  it("DashboardPage_Opened_AsksEveryReportForThisJalaliMonth", async () => {
    const api = mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    await screen.findByText("خلاصهٔ بازه");
    const month = presetRange("month", gymToday());
    for (const path of rangeReports) {
      expect(api.requestsTo("GET", path).map(queryOf)).toEqual([month]);
    }
    expect(screen.getByRole("button", { name: "این ماه" })).toHaveAttribute("aria-pressed", "true");
    expect(api.unexpected).toEqual([]);
  });

  it("DashboardPage_PresetPressed_AsksForItsRangeAndKeepsItInTheUrl", async () => {
    const api = mockApi(dashboard);
    const { router } = renderApp("/dashboard", { session: session() });
    await screen.findByText("خلاصهٔ بازه");

    fireEvent.click(screen.getByRole("button", { name: "ماه قبل" }));

    const lastMonth = presetRange("lastMonth", gymToday());
    await waitFor(() =>
      expect(api.requestsTo("GET", "/api/reports/financial").map(queryOf)).toContainEqual(
        lastMonth,
      ),
    );
    expect(router.state.location.search).toBe(`?from=${lastMonth.from}&to=${lastMonth.to}`);
    expect(screen.getByRole("button", { name: "ماه قبل" })).toHaveAttribute("aria-pressed", "true");
    // The figures of today do not depend on the range: asked once, not again.
    expect(api.requestsTo("GET", "/api/reports/needs-attention")).toHaveLength(1);
  });

  it("DashboardPage_RangeOfOnesOwnInTheUrl_OpensTheDateBoxesAndAsksForIt", async () => {
    const api = mockApi(dashboard);
    renderApp("/dashboard?from=2026-09-01&to=2026-09-15", { session: session() });

    await screen.findByText("خلاصهٔ بازه");
    expect(api.requestsTo("GET", "/api/reports/financial").map(queryOf)).toEqual([
      { from: "2026-09-01", to: "2026-09-15" },
    ]);
    expect(screen.getByLabelText("از تاریخ")).toHaveValue("۱۴۰۵/۰۶/۱۰");
    expect(screen.getByLabelText("تا تاریخ")).toHaveValue("۱۴۰۵/۰۶/۲۴");
    expect(screen.getByRole("button", { name: "دلخواه" })).toHaveAttribute("aria-pressed", "true");
  });

  it("DashboardPage_BackwardsRange_SaysSoAndAsksNoRangeReport", async () => {
    const api = mockApi(dashboard);
    renderApp("/dashboard?from=2026-09-15&to=2026-09-01", { session: session() });

    expect(await screen.findByText("بازهٔ تاریخ نامعتبر است.")).toBeInTheDocument();
    // The lists of today still come.
    await screen.findByText("نیاز به اقدام");
    for (const path of rangeReports) {
      expect(api.requestsTo("GET", path)).toHaveLength(0);
    }
  });

  it("DashboardPage_RangeOverAYear_SaysSoAndAsksNoRangeReport", async () => {
    const api = mockApi(dashboard);
    renderApp("/dashboard?from=2025-01-01&to=2026-09-01", { session: session() });

    expect(await screen.findByText("بازهٔ گزارش حداکثر یک سال (۳۶۶ روز) است.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/reports/financial")).toHaveLength(0);
  });

  it("DashboardPage_Loaded_ShowsTheFiguresAndTheirChangeSinceTheRangeBefore", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    await screen.findByText("خلاصهٔ بازه");

    const revenue = card("درآمد خالص");
    expect(revenue).toHaveTextContent("۱۲٬۰۰۰٬۰۰۰ تومان");
    expect(within(revenue).getByText(/۲۰٪ بیشتر از بازهٔ قبل/)).toHaveClass("text-success");

    // Expenses going up is bad news, whatever the arrow's direction.
    expect(within(card("هزینه‌ها")).getByText(/۲۵٪ بیشتر از بازهٔ قبل/)).toHaveClass(
      "text-destructive",
    );
    expect(card("فروش")).toHaveTextContent("بدون تغییر نسبت به بازهٔ قبل");
    // Nothing before: a percent would say nothing, so the figure before is shown.
    expect(card("سود ناخالص بوفه")).toHaveTextContent("بازهٔ قبل: ۰ تومان");
    expect(card("صندوق نقدی")).toHaveTextContent("۳٬۰۰۰٬۰۰۰ تومان");

    expect(within(card("ورود اعضا")).getByText(/۲۰٪ کمتر از بازهٔ قبل/)).toHaveClass(
      "text-destructive",
    );
    expect(card("ورود اعضا")).toHaveTextContent("۱۲ عضو مختلف");
    // 3 renewed of the 4 decided; the one still waiting is left out (§12).
    expect(card("نرخ تمدید")).toHaveTextContent("۷۵٪");
    expect(card("نرخ تمدید")).toHaveTextContent("۳ از ۴ پلن تمام‌شده؛ ۱ در انتظار");
  });

  it("DashboardPage_Loaded_ShowsThePlansAndDebtsOfToday", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    await screen.findByRole("heading", { name: "امروز" });
    expect(card("پلن فعال")).toHaveTextContent("۴۲");
    expect(card("پلن فریز")).toHaveTextContent("۳");
    expect(card("کل مطالبات")).toHaveTextContent("۳٬۵۰۰٬۰۰۰ تومان");
    expect(card("مطالبات بیش از ۳۰ روز")).toHaveTextContent("۲٬۰۰۰٬۰۰۰ تومان");
  });

  it("DashboardPage_NeedsAttention_ListsWhoToCallWithALinkToTheirProfile", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    const runningOut = await screen.findByRole("region", { name: "رو به پایان، بدون تمدید" });
    expect(within(runningOut).getByRole("link", { name: ali.fullName })).toHaveAttribute(
      "href",
      `/members/${ali.memberId}`,
    );
    expect(runningOut).toHaveTextContent("جلسه‌ای نمانده");
    expect(runningOut).toHaveTextContent("۰۹۱۲ ۱۲۳ ۴۵۶۷");

    const oldDebts = screen.getByRole("region", { name: "بدهی قدیمی" });
    expect(oldDebts).toHaveTextContent("۲٬۰۰۰٬۰۰۰ تومان");

    expect(screen.getByRole("region", { name: "مدتی است نیامده" })).toHaveTextContent(
      "کسی در این فهرست نیست.",
    );
    // Nothing owed by walk-ins or guests: the line is left out.
    expect(screen.queryByText(/مشتری آزاد بوفه و مهمان‌ها/)).not.toBeInTheDocument();
  });

  it("DashboardPage_OldDebtWithoutAMember_IsOneFigureBesideTheList", async () => {
    mockApi({
      ...dashboard,
      "GET /api/reports/needs-attention": () =>
        json(200, {
          ...needsAttention,
          absent: [{ ...ali, lastVisitOn: null, daysAway: 12 }],
          oldDebtWithoutMember: 150000,
        }),
    });
    renderApp("/dashboard", { session: session() });

    expect(
      await screen.findByText("بدهی قدیمی مشتری آزاد بوفه و مهمان‌ها: ۱۵۰٬۰۰۰ تومان"),
    ).toBeInTheDocument();
    const absent = screen.getByRole("region", { name: "مدتی است نیامده" });
    expect(absent).toHaveTextContent("۱۲ روز");
    expect(absent).toHaveTextContent("از شروع پلن نیامده");
  });

  it("DashboardPage_ChequesDue_ListsEachWithOverdueAndTodayMarked", async () => {
    mockApi({
      ...dashboard,
      "GET /api/reports/needs-attention": () =>
        json(200, {
          ...needsAttention,
          chequesDue: [
            {
              chequeId: "0199a000-0000-7000-8000-0000000000e1",
              payee: "فروشگاه تجهیزات",
              amount: 50000000,
              dueDate: "2026-09-28",
              description: "قسط اول تردمیل",
            },
            {
              chequeId: "0199a000-0000-7000-8000-0000000000e2",
              payee: "تعمیرگاه",
              amount: 8000000,
              dueDate: "2026-10-04",
              description: "تعمیر دوچرخه",
            },
          ],
        }),
    });
    renderApp("/dashboard", { session: session() });

    // §9 Cheques: past its date and not marked stays on the list, marked; the date is Jalali.
    const cheques = await screen.findByRole("region", { name: "چک‌های نزدیک سررسید" });
    const [overdue, dueToday] = within(cheques).getAllByRole("listitem");
    expect(overdue).toHaveTextContent("فروشگاه تجهیزات");
    expect(overdue).toHaveTextContent("۵۰٬۰۰۰٬۰۰۰ تومان");
    expect(overdue).toHaveTextContent("سررسید گذشته، ۱۴۰۵/۰۷/۰۶");
    expect(dueToday).toHaveTextContent("امروز، ۱۴۰۵/۰۷/۱۲");
    expect(within(cheques).getByRole("link", { name: "همهٔ چک‌ها" })).toHaveAttribute(
      "href",
      "/cheques",
    );
  });

  it("DashboardPage_NoChequesDue_SaysTheListIsEmpty", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    expect(await screen.findByRole("region", { name: "چک‌های نزدیک سررسید" })).toHaveTextContent(
      "چکی در این فهرست نیست.",
    );
  });

  it("DashboardPage_Loaded_ShowsMoneyByStaffMemberAndTheWeekdayHourTable", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    const staffTable = (await screen.findByText("دریافتی به تفکیک کارمند")).closest(
      "[data-slot=card]",
    ) as HTMLElement;
    const row = within(staffTable).getByText("سارا رضایی").closest("tr") as HTMLElement;
    expect(row).toHaveTextContent("۱۳٬۰۰۰٬۰۰۰ تومان");
    expect(row).toHaveTextContent("۱٬۰۰۰٬۰۰۰ تومان");

    // Sunday 18:00 is the only busy hour, so it is the only column.
    expect(screen.getByTitle("یکشنبه، ساعت ۱۸: ۴۰ ورود")).toHaveTextContent("۴۰");
    expect(screen.getByTitle("شنبه، ساعت ۱۸: ۰ ورود")).toHaveTextContent("");
  });

  it("DashboardPage_OwnerMenu_LinksToTheDashboard", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    expect(await screen.findByRole("link", { name: /داشبورد/ })).toHaveAttribute(
      "href",
      "/dashboard",
    );
  });
});
