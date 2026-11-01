import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess, trainer } from "../test/renderSite";

// A Saturday: 1405/08/09.
const today = "2026-10-31";

const schedule = {
  startsOn: "2026-10-24",
  endsOn: "2026-12-18",
  slots: [
    { day: "Saturday", start: "13:00:00", end: "14:00:00" },
    { day: "Monday", start: "13:00:00", end: "14:00:00" },
  ],
};

const place = (date: string, extra: Record<string, boolean> = {}) => ({
  date,
  start: "13:00:00",
  end: "14:00:00",
  moved: false,
  removed: false,
  started: false,
  left: false,
  ...extra,
});

const sessions = [
  place(today, { started: true }),
  place("2026-11-02"),
  place("2026-11-07", { removed: true }),
  place("2026-11-09"),
];

const openings = [
  { date: "2026-11-03", start: "17:00:00", end: "18:00:00", free: 2 },
  { date: "2026-11-03", start: "18:00:00", end: "19:00:00", free: 5 },
];

function mine(list = sessions) {
  return { today, trainerName: trainer.fullName, schedule, sessions: list, openings };
}

async function bodyOf(request: Request) {
  return (await request.json()) as unknown;
}

describe("the member's «سانس‌های من»", () => {
  it("Home_WithSessions_ShowsTheNextThreeAndTheWayIn", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine()),
    });
    renderSite("/", { access: siteAccess() });

    const next = within(await screen.findByRole("list", { name: "سانس‌های بعدی" }));
    expect(next.getAllByRole("listitem")).toHaveLength(3);
    expect(next.getAllByRole("listitem")[0]).toHaveTextContent("شنبه ۹ آبان۱۳:۰۰ تا ۱۴:۰۰");
    expect(next.getAllByRole("listitem")[2]).toHaveTextContent("لغو شد");
    expect(screen.getByRole("link", { name: "همهٔ سانس‌ها" })).toHaveAttribute("href", "/sessions");
  });

  it("Home_ADateLeft_IsNotAmongTheNext", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () =>
        json(200, mine([place("2026-11-02", { left: true }), place("2026-11-09")])),
    });
    renderSite("/", { access: siteAccess() });

    const next = within(await screen.findByRole("list", { name: "سانس‌های بعدی" }));
    expect(next.getByRole("listitem")).toHaveTextContent("دوشنبه ۱۸ آبان");
  });

  it("Home_EveryDateLeft_StillLeadsToThePage", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine([place("2026-11-02", { left: true })])),
    });
    renderSite("/", { access: siteAccess() });

    expect(await screen.findByText("سانسی در پیش ندارید.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "همهٔ سانس‌ها" })).toHaveAttribute("href", "/sessions");
  });

  it("Home_TrainerWithNoScheduleYet_SaysSo", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, { ...mine([]), schedule: null, openings: [] }),
    });
    renderSite("/", { access: siteAccess() });

    expect(
      await screen.findByText("مربی‌تان هنوز برنامهٔ سانسی برایتان نچیده است."),
    ).toBeInTheDocument();
  });

  it("Home_NoTrainer_ShowsNoSessionsCard", async () => {
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, siteAccess().me) });
    renderSite("/", { access: siteAccess() });

    await screen.findByRole("heading", { name: "مربی من" });
    await vi.waitFor(() => expect(screen.getByText("هنوز مربی ندارید.")).toBeInTheDocument());
    expect(screen.queryByRole("heading", { name: "سانس‌های من" })).not.toBeInTheDocument();
  });

  it("Page_Dates_ShownOneWeekAtATimeFromToday", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine()),
    });
    renderSite("/sessions", { access: siteAccess() });

    const firstWeek = within(await screen.findByRole("list", { name: "سانس‌های پیش رو" }));
    expect(screen.getByText("هفتهٔ ۱ از ۲")).toBeInTheDocument();
    expect(
      firstWeek.getAllByRole("listitem").map((item) => item.getAttribute("aria-label")),
    ).toEqual(["شنبه ۹ آبان، ۱۳:۰۰ تا ۱۴:۰۰", "دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰"]);

    fireEvent.click(screen.getByRole("button", { name: "هفتهٔ بعد" }));

    const secondWeek = within(screen.getByRole("list", { name: "سانس‌های پیش رو" }));
    expect(
      secondWeek.getAllByRole("listitem").map((item) => item.getAttribute("aria-label")),
    ).toEqual(["شنبه ۱۶ آبان، ۱۳:۰۰ تا ۱۴:۰۰", "دوشنبه ۱۸ آبان، ۱۳:۰۰ تا ۱۴:۰۰"]);
  });

  it("Page_ShowsTheScheduleAndOnlyNotStartedDatesCanChange", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine()),
    });
    renderSite("/sessions", { access: siteAccess() });

    expect(await screen.findByText(`مربی: ${trainer.fullName}`)).toBeInTheDocument();
    expect(screen.getByRole("list", { name: "سانس‌های هفتگی" })).toHaveTextContent(
      "شنبه ۱۳:۰۰ تا ۱۴:۰۰",
    );
    const started = within(screen.getByRole("listitem", { name: "شنبه ۹ آبان، ۱۳:۰۰ تا ۱۴:۰۰" }));
    expect(started.getByText("شروع شده")).toBeInTheDocument();
    expect(started.queryByRole("button")).not.toBeInTheDocument();
    const next = within(screen.getByRole("listitem", { name: "دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰" }));
    expect(next.getByRole("button", { name: "نمی‌آیم" })).toBeInTheDocument();
    expect(next.getByRole("button", { name: "جابه‌جایی" })).toBeInTheDocument();
  });

  it("Leave_AsksFirst_ThenSendsThatDate_AndItStaysToTakeBack", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine()),
      "POST /api/site/me/sessions/leave": () =>
        json(
          200,
          mine(
            sessions.map((item) => (item.date === "2026-11-02" ? { ...item, left: true } : item)),
          ),
        ),
    });
    renderSite("/sessions", { access: siteAccess() });

    const row = within(
      await screen.findByRole("listitem", { name: "دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰" }),
    );
    fireEvent.click(row.getByRole("button", { name: "نمی‌آیم" }));
    const confirm = await screen.findByRole("dialog", { name: "نمی‌آیم" });
    expect(confirm).toHaveTextContent("جایتان آزاد می‌شود");
    fireEvent.click(within(confirm).getByRole("button", { name: "نمی‌آیم" }));

    const left = within(
      await screen.findByRole("listitem", { name: "دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰" }),
    );
    expect(await left.findByRole("button", { name: "می‌آیم" })).toBeInTheDocument();
    expect(left.getByText("نمی‌آیم")).toBeInTheDocument();
    expect(left.queryByRole("button", { name: "جابه‌جایی" })).not.toBeInTheDocument();
    const [sent] = api.requestsTo("POST", "/api/site/me/sessions/leave");
    expect(await bodyOf(sent!)).toEqual({ date: "2026-11-02", start: "13:00:00", end: "14:00:00" });
  });

  it("Return_SendsThatDate_WithoutAsking_AndItIsBack", async () => {
    const leftOne = sessions.map((item) =>
      item.date === "2026-11-02" ? { ...item, left: true } : item,
    );
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine(leftOne)),
      "POST /api/site/me/sessions/return": () => json(200, mine()),
    });
    renderSite("/sessions", { access: siteAccess() });

    const row = within(
      await screen.findByRole("listitem", { name: "دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰" }),
    );
    fireEvent.click(row.getByRole("button", { name: "می‌آیم" }));

    expect(await row.findByRole("button", { name: "نمی‌آیم" })).toBeInTheDocument();
    const [sent] = api.requestsTo("POST", "/api/site/me/sessions/return");
    expect(await bodyOf(sent!)).toEqual({ date: "2026-11-02", start: "13:00:00", end: "14:00:00" });
  });

  it("Return_ASessionFilledMeanwhile_SaysSo", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine([place("2026-11-02", { left: true })])),
      "POST /api/site/me/sessions/return": () => problem(422, "Bookings.SessionFull"),
    });
    renderSite("/sessions", { access: siteAccess() });

    fireEvent.click(await screen.findByRole("button", { name: "می‌آیم" }));

    expect(await screen.findByText("این سانس جای خالی ندارد.")).toBeInTheDocument();
  });

  it("Move_PicksASessionWithRoom_SendsBothDates", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine()),
      "POST /api/site/me/sessions/move": () => json(200, mine()),
    });
    renderSite("/sessions", { access: siteAccess() });

    const row = within(
      await screen.findByRole("listitem", { name: "دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰" }),
    );
    fireEvent.click(row.getByRole("button", { name: "جابه‌جایی" }));
    const dialog = within(await screen.findByRole("dialog", { name: "جابه‌جایی" }));
    const moveButton = dialog.getByRole("button", { name: "جابه‌جایی به این سانس" });
    expect(moveButton).toBeDisabled();
    const target = dialog.getByRole("button", { name: "سه‌شنبه ۱۲ آبان، ۱۸:۰۰ تا ۱۹:۰۰" });
    expect(target).toHaveTextContent("۵ جا");
    fireEvent.click(target);
    fireEvent.click(moveButton);

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    const [sent] = api.requestsTo("POST", "/api/site/me/sessions/move");
    expect(await bodyOf(sent!)).toEqual({
      date: "2026-11-02",
      start: "13:00:00",
      end: "14:00:00",
      toDate: "2026-11-03",
      toStart: "18:00:00",
      toEnd: "19:00:00",
    });
  });

  it("Move_Refused_ShowsTheReasonInPersian", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () => json(200, mine()),
      "POST /api/site/me/sessions/move": () => problem(422, "Bookings.SessionFull"),
    });
    renderSite("/sessions", { access: siteAccess() });

    const row = within(
      await screen.findByRole("listitem", { name: "دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰" }),
    );
    fireEvent.click(row.getByRole("button", { name: "جابه‌جایی" }));
    const dialog = within(await screen.findByRole("dialog", { name: "جابه‌جایی" }));
    fireEvent.click(dialog.getByRole("button", { name: "سه‌شنبه ۱۲ آبان، ۱۷:۰۰ تا ۱۸:۰۰" }));
    fireEvent.click(dialog.getByRole("button", { name: "جابه‌جایی به این سانس" }));

    expect(await dialog.findByText("این سانس جای خالی ندارد.")).toBeInTheDocument();
  });

  it("Page_ATrainerWhoIsNotAMember_IsSentHome", async () => {
    const trainerOnly = { member: null, trainer };
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, trainerOnly) });

    const { router } = renderSite("/sessions", { access: siteAccess(trainerOnly) });

    await vi.waitFor(() => expect(router.state.location.pathname).toBe("/"));
  });
});
