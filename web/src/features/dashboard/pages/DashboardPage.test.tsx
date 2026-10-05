import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import { json, mockApi, owner, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { ali, financialReport, needsAttention, reportHandlers } from "@/test/reports";
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
  // The route loads the page lazily (router.tsx). Loading it once here, before any test, keeps
  // that first load out of the tests' own one-second waits: on a busy machine the first test of
  // the file otherwise ran out of time while the module was still being transformed.
  beforeAll(async () => {
    await import("./DashboardPage");
  });

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
    // §12: «فروش» leaves فروشگاه and آنالیز out, and its name says so (1405/07/14).
    expect(card("فروش (به غیر از آنالیز و فروشگاه)")).toHaveTextContent(
      "بدون تغییر نسبت به بازهٔ قبل",
    );
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

  it("DashboardPage_Profit_IsShownInGreen", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    await screen.findByText("خلاصهٔ بازه");
    // §12: a profit is green, the net one and the cafe's alike.
    expect(within(card("سود خالص")).getByText("۷٬۰۰۰٬۰۰۰ تومان")).toHaveClass("text-success");
    expect(within(card("سود ناخالص بوفه")).getByText("۸۰۰٬۰۰۰ تومان")).toHaveClass("text-success");
  });

  it("DashboardPage_LossAndZero_LossIsRedWithItsMinusSignZeroIsNeither", async () => {
    mockApi({
      ...dashboard,
      "GET /api/reports/financial": () =>
        json(200, {
          ...financialReport,
          current: { ...financialReport.current, netProfit: -30500000, cafeGrossProfit: 0 },
        }),
    });
    renderApp("/dashboard", { session: session() });

    await screen.findByText("خلاصهٔ بازه");
    // §12: a month with more expenses than revenue is a loss, shown as one, not as «—».
    const loss = within(card("سود خالص")).getByText(/۳۰٬۵۰۰٬۰۰۰ تومان$/);
    expect(loss.textContent).toMatch(/^\u200e\u2212۳۰٬۵۰۰٬۰۰۰ تومان$/);
    expect(loss).toHaveClass("text-destructive");
    const zero = within(card("سود ناخالص بوفه")).getByText("۰ تومان");
    expect(zero).not.toHaveClass("text-success");
    expect(zero).not.toHaveClass("text-destructive");
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

  it("DashboardPage_PayablesDue_ListsEachWithKindOverdueAndTodayMarked", async () => {
    mockApi({
      ...dashboard,
      "GET /api/reports/needs-attention": () =>
        json(200, {
          ...needsAttention,
          payablesDue: [
            {
              payableId: "0199a000-0000-7000-8000-0000000000e1",
              kind: "Cheque",
              payee: "فروشگاه تجهیزات",
              amount: 50000000,
              dueDate: "2026-09-28",
              description: "تردمیل",
              installmentNumber: null,
              installmentCount: null,
            },
            {
              payableId: "0199a000-0000-7000-8000-0000000000e2",
              kind: "Installment",
              payee: "بانک ملت",
              amount: 8000000,
              dueDate: "2026-10-04",
              description: "وام دستگاه",
              installmentNumber: 2,
              installmentCount: 12,
            },
          ],
        }),
    });
    renderApp("/dashboard", { session: session() });

    // §9: past its date and not marked stays on the list, marked; the date is Jalali.
    const payables = await screen.findByRole("region", { name: "چک و قسط نزدیک سررسید" });
    const [overdue, dueToday] = within(payables).getAllByRole("listitem");
    expect(overdue).toHaveTextContent("فروشگاه تجهیزات");
    expect(overdue).toHaveTextContent("چک — تردمیل");
    expect(overdue).toHaveTextContent("۵۰٬۰۰۰٬۰۰۰ تومان");
    expect(overdue).toHaveTextContent("سررسید گذشته، ۱۴۰۵/۰۷/۰۶");
    expect(dueToday).toHaveTextContent("قسط ۲ از ۱۲ — وام دستگاه");
    expect(dueToday).toHaveTextContent("امروز، ۱۴۰۵/۰۷/۱۲");
    expect(within(payables).getByRole("link", { name: "همهٔ چک‌ها و قسط‌ها" })).toHaveAttribute(
      "href",
      "/payables",
    );
  });

  it("DashboardPage_NoPayablesDue_SaysTheListIsEmpty", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    expect(await screen.findByRole("region", { name: "چک و قسط نزدیک سررسید" })).toHaveTextContent(
      "چک یا قسطی در این فهرست نیست.",
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

  it("DashboardPage_RevenueBySource_ListsTheCafeAboveTheShopAndCountsPlansAndSingleVisitsSold", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    const chart = (await screen.findByText("درآمد به تفکیک منبع")).closest(
      "[data-slot=card]",
    ) as HTMLElement;
    const rows = within(chart).getAllByRole("listitem");

    // The cafe above فروشگاه (asked by the developer, 1405/07/13).
    expect(rows.map((row) => row.firstElementChild?.textContent)).toEqual([
      "پلن",
      "تک‌جلسه‌ای",
      "هوازی",
      "بوفه",
      "فروشگاه",
      "آنالیز",
    ]);

    // How many were sold in the range, on the plan and single-visit bars only.
    expect(rows[0]).toHaveTextContent("۱۲ پلن فروخته شد");
    expect(rows[0]).toHaveTextContent("۹٬۰۰۰٬۰۰۰ تومان");
    expect(rows[1]).toHaveTextContent("۴ تک‌جلسه فروخته شد");
    expect(rows[3]).not.toHaveTextContent("فروخته شد");
  });

  it("DashboardPage_NewMembers_SayTheyAreCountedOnTheirFirstPlansSale", async () => {
    mockApi(dashboard);
    renderApp("/dashboard", { session: session() });

    expect(
      await screen.findByText("عضوهایی که اولین پلن عضویتشان در این بازه فروخته شده"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("عضوهایی که اولین پلن عضویتشان در این ماه فروخته شده"),
    ).toBeInTheDocument();
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
