import { act, fireEvent, screen, waitFor } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

import type { TodaySummary } from "../api";

const todayPath = "/api/reports/today";

function summary(overrides: Partial<TodaySummary> = {}): TodaySummary {
  return {
    today: gymToday(),
    insideNow: 23,
    guestsInside: 2,
    checkIns: 87,
    received: 12_450_000,
    plansSold: 4,
    singleVisitsSold: 9,
    ...overrides,
  };
}

/** The card a label heads: its `dt`'s box, figure and hint included. */
function card(label: string) {
  const term = screen.getAllByText(label).find((element) => element.tagName === "DT");
  if (term === undefined || term.parentElement === null) {
    throw new Error(`No card is labelled ${label}`);
  }
  return term.parentElement;
}

describe("TodaySummaryPage", () => {
  // Loaded once before the tests, as the dashboard's tests do: the route loads it lazily.
  beforeAll(async () => {
    await import("./TodaySummaryPage");
  });

  it("TodaySummaryPage_Owner_ShowsTodaysFiguresInPersianDigits", async () => {
    mockApi({ ...signedInHandlers(owner), [`GET ${todayPath}`]: () => json(200, summary()) });

    renderApp("/today", { session: session() });

    expect(await screen.findByText("۲۳ نفر")).toBeInTheDocument();
    expect(card("داخل باشگاه، همین الان")).toHaveTextContent("۲ نفرشان مهمان‌اند");
    expect(card("دریافتی امروز")).toHaveTextContent("۱۲٬۴۵۰٬۰۰۰ تومان");
    expect(card("ورود امروز")).toHaveTextContent("۸۷");
    expect(card("پلن")).toHaveTextContent("۴");
    expect(card("تک‌جلسه")).toHaveTextContent("۹");
    expect(screen.getByText(/به‌روز در/)).toBeInTheDocument();
  });

  it("TodaySummaryPage_NoGuestInside_SaysNothingAboutGuests", async () => {
    mockApi({
      ...signedInHandlers(owner),
      [`GET ${todayPath}`]: () => json(200, summary({ insideNow: 5, guestsInside: 0 })),
    });

    renderApp("/today", { session: session() });

    expect(await screen.findByText("۵ نفر")).toBeInTheDocument();
    expect(screen.queryByText(/مهمان‌اند/)).not.toBeInTheDocument();
  });

  it("TodaySummaryPage_RefreshPressed_AsksAgainAndShowsTheNewFigures", async () => {
    let inside = 23;
    const api = mockApi({
      ...signedInHandlers(owner),
      [`GET ${todayPath}`]: () => json(200, summary({ insideNow: inside })),
    });
    renderApp("/today", { session: session() });
    await screen.findByText("۲۳ نفر");

    inside = 24;
    fireEvent.click(screen.getByRole("button", { name: "به‌روزرسانی" }));

    expect(await screen.findByText("۲۴ نفر")).toBeInTheDocument();
    expect(api.requestsTo("GET", todayPath)).toHaveLength(2);
  });

  it("TodaySummaryPage_PhoneComesBackToThePage_AsksAgain", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      [`GET ${todayPath}`]: () => json(200, summary()),
    });
    renderApp("/today", { session: session() });
    await screen.findByText("۲۳ نفر");

    // What unlocking the phone or switching back to the app does: the page becomes visible again.
    act(() => {
      window.dispatchEvent(new Event("visibilitychange"));
    });

    await waitFor(() => expect(api.requestsTo("GET", todayPath)).toHaveLength(2));
  });

  it("TodaySummaryPage_RefreshFails_KeepsTheFiguresAndSaysHowOldTheyAre", async () => {
    let fail = false;
    mockApi({
      ...signedInHandlers(owner),
      [`GET ${todayPath}`]: () => (fail ? problem(500, "Test.Down") : json(200, summary())),
    });
    renderApp("/today", { session: session() });
    await screen.findByText("۲۳ نفر");

    fail = true;
    fireEvent.click(screen.getByRole("button", { name: "به‌روزرسانی" }));

    // The app tries a failed request once more (queryClient.ts) before it says so.
    expect(
      await screen.findByText(/عددهای زیر مال ساعت/, undefined, { timeout: 4000 }),
    ).toBeInTheDocument();
    expect(screen.getByText("۲۳ نفر")).toBeInTheDocument();
    // The date stays, so figures left from yesterday cannot pass for today's.
    expect(screen.getByText(/به‌روز در/)).toBeInTheDocument();
  });

  it("TodaySummaryPage_RoleUnknownBecauseTheRequestFailed_StaysOnThePage", async () => {
    mockApi({
      ...signedInHandlers(owner),
      "GET /api/auth/me": () => problem(500, "Test.Down"),
    });

    const { router } = renderApp("/today", { session: session() });

    // The app tries a failed request once more (queryClient.ts) before it says so.
    expect(
      await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.", undefined, { timeout: 4000 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/today");
  });

  it("TodaySummaryPage_Staff_IsSentToTheLockerMapWithoutAsking", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    // The phone's long-press shortcut leads here on a Staff member's phone too.
    const { router } = renderApp("/today", { session: session() });

    await waitFor(() => expect(router.state.location.pathname).toBe("/"));
    expect(screen.queryByText("اجازهٔ دسترسی به این بخش را ندارید.")).not.toBeInTheDocument();
    expect(api.requestsTo("GET", todayPath)).toHaveLength(0);
  });

  it("TodaySummaryPage_Menu_HasTheItemForTheOwnerOnly", async () => {
    mockApi(signedInHandlers(staffUser));
    const { unmount } = renderApp("/", { session: session() });
    await screen.findByRole("link", { name: /ورود با کمد/ });
    expect(screen.queryByRole("link", { name: /خلاصهٔ امروز/ })).not.toBeInTheDocument();
    unmount();

    mockApi(signedInHandlers(owner));
    renderApp("/", { session: session() });
    expect(await screen.findByRole("link", { name: /خلاصهٔ امروز/ })).toHaveAttribute(
      "href",
      "/today",
    );
  });
});
