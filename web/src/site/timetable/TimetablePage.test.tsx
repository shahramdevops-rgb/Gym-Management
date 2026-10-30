import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import {
  activeMember,
  emptyHome,
  emptyTimetable,
  renderSite,
  siteAccess,
  trainer,
} from "../test/renderSite";

const trainerOnly = { member: null, trainer };

// A Saturday: 1405/08/09.
const today = "2026-10-31";
const tomorrow = "2026-11-01";

const saturdayPattern = {
  startsOn: tomorrow,
  sessions: [
    { day: "Saturday", start: "13:00:00", end: "14:00:00", capacity: 8 },
    { day: "Saturday", start: "14:00:00", end: "15:00:00", capacity: 6 },
  ],
};

/** The timetable with today's sessions, and `changes` on the dates it names. */
function timetableWith(
  pattern: typeof saturdayPattern | null,
  changes: Record<string, { changedByHand: boolean; sessions: unknown[] }> = {},
) {
  const empty = emptyTimetable(today);

  return {
    ...empty,
    pattern,
    days: empty.days.map((day) => ({ ...day, ...changes[day.date] })),
  };
}

const todaysSessions = {
  [today]: {
    changedByHand: false,
    sessions: [{ start: "13:00:00", end: "14:00:00", capacity: 8 }],
  },
};

async function bodyOf(request: Request) {
  return (await request.json()) as unknown;
}

describe("the trainer's timetable", () => {
  it("Home_Trainer_ShowsTodaysSessionsAndTheWayIn", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern, todaysSessions)),
    });

    renderSite("/", { access: siteAccess(trainerOnly) });

    const list = await screen.findByRole("list", { name: "سانس‌های امروز" });
    expect(list).toHaveTextContent("۱۳:۰۰ تا ۱۴:۰۰ · ۸ نفر");
    expect(screen.getByRole("link", { name: "برنامه سانس‌ها" })).toHaveAttribute(
      "href",
      "/timetable",
    );
  });

  it("Home_TrainerWithNoPattern_SaysSo", async () => {
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, trainerOnly) });

    renderSite("/", { access: siteAccess(trainerOnly) });

    expect(await screen.findByText("هنوز برنامهٔ سانسی نساخته‌اید.")).toBeInTheDocument();
  });

  it("Page_AMemberWhoIsNotATrainer_IsSentHome", async () => {
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, siteAccess().me) });

    const { router } = renderSite("/timetable", { access: siteAccess() });

    await vi.waitFor(() => expect(router.state.location.pathname).toBe("/"));
    expect(await screen.findByText(`سلام ${activeMember.fullName}`)).toBeInTheDocument();
  });

  it("Page_Pattern_ShowsEachDayOfTheWeekFromSaturday", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern)),
    });

    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    const pattern = await screen.findByRole("list", { name: "الگوی هفتگی" });
    const days = within(pattern).getAllByRole("listitem");
    expect(days.map((day) => day.firstChild?.textContent)).toEqual([
      "شنبه",
      "یکشنبه",
      "دوشنبه",
      "سه‌شنبه",
      "چهارشنبه",
      "پنجشنبه",
      "جمعه",
    ]);
    expect(days[0]).toHaveTextContent("۱۳:۰۰ تا ۱۴:۰۰ · ۸ نفر");
    expect(days[0]).toHaveTextContent("۱۴:۰۰ تا ۱۵:۰۰ · ۶ نفر");
    expect(days[1]).toHaveTextContent("بدون سانس");
    expect(screen.getByText("از ۱۴۰۵/۰۸/۱۰، هر هفته تکرار می‌شود.")).toBeInTheDocument();
  });

  it("BuildPattern_ASessionOnSaturday_SendsItFromTomorrow", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(null)),
      "PUT /api/site/timetable/pattern": () =>
        json(200, timetableWith({ startsOn: tomorrow, sessions: [saturdayPattern.sessions[0]!] })),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "ساختن الگو" }));
    const saturday = within(screen.getByRole("region", { name: "شنبه" }));
    fireEvent.click(saturday.getByRole("button", { name: "افزودن سانس به شنبه" }));
    fireEvent.change(saturday.getByRole("combobox", { name: "ساعت شروع" }), {
      target: { value: "13" },
    });
    fireEvent.change(saturday.getByRole("combobox", { name: "ساعت پایان" }), {
      target: { value: "14" },
    });
    fireEvent.change(saturday.getByRole("combobox", { name: "ظرفیت" }), { target: { value: "8" } });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره الگو" }));

    expect(await screen.findByRole("list", { name: "الگوی هفتگی" })).toHaveTextContent(
      "۱۳:۰۰ تا ۱۴:۰۰ · ۸ نفر",
    );
    const [sent] = api.requestsTo("PUT", "/api/site/timetable/pattern");
    expect(await bodyOf(sent!)).toEqual({
      startsOn: tomorrow,
      sessions: [{ day: "Saturday", start: "13:00:00", end: "14:00:00", capacity: 8 }],
    });
  });

  it("AddSession_AfterAnother_StartsWhereTheLastOneEnds", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern)),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر الگو" }));
    const saturday = within(screen.getByRole("region", { name: "شنبه" }));
    fireEvent.click(saturday.getByRole("button", { name: "افزودن سانس به شنبه" }));

    const third = within(saturday.getByRole("listitem", { name: "سانس ۳" }));
    expect(third.getByRole("combobox", { name: "ساعت شروع" })).toHaveValue("15");
    expect(third.getByRole("combobox", { name: "ساعت پایان" })).toHaveValue("16");
    expect(third.getByRole("combobox", { name: "ظرفیت" })).toHaveValue("6");
  });

  it("SavePattern_Refused_ShowsTheReasonInPersian", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern)),
      "PUT /api/site/timetable/pattern": () => problem(400, "Timetable.EndNotAfterStart"),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر الگو" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره الگو" }));

    expect(
      await screen.findByText("پایان سانس باید بعد از شروع آن و در همان روز باشد."),
    ).toBeInTheDocument();
  });

  it("Weeks_NextWeek_ShowsItsSevenDays", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern, todaysSessions)),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    const days = await screen.findByRole("list", { name: "روزهای هفته" });
    expect(within(days).getAllByRole("listitem")).toHaveLength(7);
    expect(within(days).getAllByRole("listitem")[0]).toHaveTextContent("شنبه ۹ آبان");
    expect(within(days).getAllByRole("listitem")[0]).toHaveTextContent("امروز");
    expect(screen.getByRole("button", { name: "هفتهٔ قبل" })).toBeDisabled();

    fireEvent.click(screen.getByRole("button", { name: "هفتهٔ بعد" }));

    expect(screen.getByText("هفتهٔ ۲ از ۱۲")).toBeInTheDocument();
    expect(within(days).getAllByRole("listitem")[0]).toHaveTextContent("شنبه ۱۶ آبان");
  });

  it("Day_CloseIt_SendsNoSessionsForThatDateOnly", async () => {
    const date = "2026-11-02";
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () =>
        json(
          200,
          timetableWith(saturdayPattern, {
            [date]: { changedByHand: false, sessions: [saturdayPattern.sessions[0]] },
          }),
        ),
      [`PUT /api/site/timetable/days/${date}`]: () =>
        json(
          200,
          timetableWith(saturdayPattern, { [date]: { changedByHand: true, sessions: [] } }),
        ),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.queryByRole("button", { name: "برگرداندن به الگو" })).not.toBeInTheDocument();
    fireEvent.click(dialog.getByRole("button", { name: "تعطیل کردن این روز" }));

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByRole("button", { name: /دوشنبه ۱۱ آبان/ })).toHaveTextContent("تعطیل");
    const [sent] = api.requestsTo("PUT", `/api/site/timetable/days/${date}`);
    expect(await bodyOf(sent!)).toEqual({ sessions: [] });
  });

  it("Day_ChangedByHand_CanGoBackToThePattern", async () => {
    const date = "2026-11-02";
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () =>
        json(
          200,
          timetableWith(saturdayPattern, { [date]: { changedByHand: true, sessions: [] } }),
        ),
      [`DELETE /api/site/timetable/days/${date}`]: () => json(200, timetableWith(saturdayPattern)),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    const day = await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ });
    expect(day).toHaveTextContent("تغییر جداگانه");
    fireEvent.click(day);
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "برگرداندن به الگو" }),
    );

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("DELETE", `/api/site/timetable/days/${date}`)).toHaveLength(1);
    expect(screen.getByRole("button", { name: /دوشنبه ۱۱ آبان/ })).not.toHaveTextContent(
      "تغییر جداگانه",
    );
  });
});
