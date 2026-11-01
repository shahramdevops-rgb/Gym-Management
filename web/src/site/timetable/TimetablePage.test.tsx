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
    sessions: [{ start: "13:00:00", end: "14:00:00", capacity: 8, students: 3 }],
  },
};

/** A change's check that touches no student: saved without asking. */
const noImpact = () => json(200, { students: 0, sessions: [] });

/** A date's «شاگردان سانس» with no session. */
const noStudents = (date: string) => () => json(200, { date, sessions: [] });

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
    expect(list).toHaveTextContent("۱۳:۰۰ تا ۱۴:۰۰ · ۳ از ۸ نفر");
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
      "POST /api/site/timetable/pattern/check": noImpact,
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

  it("CopyDay_ToTwoDays_SendsTheSameSessionsForEachOnSave", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern)),
      "POST /api/site/timetable/pattern/check": noImpact,
      "PUT /api/site/timetable/pattern": () => json(200, timetableWith(saturdayPattern)),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر الگو" }));
    fireEvent.click(screen.getByRole("button", { name: "کپی شنبه به روزهای دیگر" }));
    const copy = within(screen.getByRole("group", { name: "کپی شنبه به روزهای دیگر" }));
    expect(copy.queryByRole("button", { name: "شنبه" })).not.toBeInTheDocument();
    fireEvent.click(copy.getByRole("button", { name: "دوشنبه" }));
    fireEvent.click(copy.getByRole("button", { name: "چهارشنبه" }));
    fireEvent.click(copy.getByRole("button", { name: "کپی" }));

    expect(
      screen.queryByRole("group", { name: "کپی شنبه به روزهای دیگر" }),
    ).not.toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/timetable/pattern/check")).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "ذخیره الگو" }));

    await vi.waitFor(() =>
      expect(api.requestsTo("PUT", "/api/site/timetable/pattern")).toHaveLength(1),
    );
    const [sent] = api.requestsTo("PUT", "/api/site/timetable/pattern");
    const sessionsOf = (day: string) =>
      saturdayPattern.sessions.map((session) => ({ ...session, day }));
    expect(await bodyOf(sent!)).toEqual({
      startsOn: tomorrow,
      sessions: [...sessionsOf("Saturday"), ...sessionsOf("Monday"), ...sessionsOf("Wednesday")],
    });
  });

  it("CopyDay_ToADayWithSessions_TakesTheirPlace", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () =>
        json(
          200,
          timetableWith({
            startsOn: tomorrow,
            sessions: [
              ...saturdayPattern.sessions,
              { day: "Sunday", start: "09:00:00", end: "12:00:00", capacity: 4 },
            ],
          }),
        ),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر الگو" }));
    fireEvent.click(screen.getByRole("button", { name: "کپی شنبه به روزهای دیگر" }));
    const copy = within(screen.getByRole("group", { name: "کپی شنبه به روزهای دیگر" }));
    fireEvent.click(copy.getByRole("button", { name: "یکشنبه" }));
    fireEvent.click(copy.getByRole("button", { name: "کپی" }));

    const sunday = within(screen.getByRole("list", { name: "سانس‌های یکشنبه" }));
    const values = (name: string) =>
      sunday.getAllByRole("combobox", { name }).map((box) => (box as HTMLSelectElement).value);
    expect(values("ساعت شروع")).toEqual(["13", "14"]);
    expect(values("ساعت پایان")).toEqual(["14", "15"]);
    expect(values("ظرفیت")).toEqual(["8", "6"]);
  });

  it("CopyDay_DayWithNoSessions_HasNoCopyButton", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern)),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر الگو" }));

    expect(screen.getByRole("button", { name: "کپی شنبه به روزهای دیگر" })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "کپی یکشنبه به روزهای دیگر" }),
    ).not.toBeInTheDocument();
  });

  it("SavePattern_Refused_ShowsTheReasonInPersian", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern)),
      "POST /api/site/timetable/pattern/check": () => problem(400, "Timetable.EndNotAfterStart"),
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
      [`GET /api/site/timetable/days/${date}/students`]: noStudents(date),
      [`POST /api/site/timetable/days/${date}/check`]: noImpact,
      [`PUT /api/site/timetable/days/${date}`]: () =>
        json(
          200,
          timetableWith(saturdayPattern, { [date]: { changedByHand: true, sessions: [] } }),
        ),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.click(dialog.getByRole("button", { name: "تغییر سانس‌های این روز" }));
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
      [`GET /api/site/timetable/days/${date}/students`]: noStudents(date),
      [`GET /api/site/timetable/days/${date}/reset-check`]: noImpact,
      [`DELETE /api/site/timetable/days/${date}`]: () => json(200, timetableWith(saturdayPattern)),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    const day = await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ });
    expect(day).toHaveTextContent("تغییر جداگانه");
    fireEvent.click(day);
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.click(dialog.getByRole("button", { name: "تغییر سانس‌های این روز" }));
    fireEvent.click(dialog.getByRole("button", { name: "برگرداندن به الگو" }));

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("DELETE", `/api/site/timetable/days/${date}`)).toHaveLength(1);
    expect(screen.getByRole("button", { name: /دوشنبه ۱۱ آبان/ })).not.toHaveTextContent(
      "تغییر جداگانه",
    );
  });
});

describe("students in the trainer's sessions", () => {
  const date = "2026-11-02";
  const touched = {
    students: 2,
    sessions: [
      { date, start: "13:00:00", end: "14:00:00", students: 2, removed: true },
      { date: "2026-11-09", start: "14:00:00", end: "15:00:00", students: 1, removed: false },
    ],
  };

  it("SavePattern_TouchesStudents_WarnsAndSavesOnlyOnYes", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern)),
      "POST /api/site/timetable/pattern/check": () => json(200, touched),
      "PUT /api/site/timetable/pattern": () => json(200, timetableWith(saturdayPattern)),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر الگو" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره الگو" }));

    const warning = await screen.findByRole("alert");
    expect(warning).toHaveTextContent("این تغییر به ۲ شاگرد می‌رسد");
    const sessions = within(warning).getAllByRole("listitem");
    expect(sessions[0]).toHaveTextContent(
      "دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰: ۲ شاگرد، سانس لغو می‌شود",
    );
    expect(sessions[1]).toHaveTextContent("ظرفیت از شاگردانش کمتر می‌شود");
    expect(api.requestsTo("PUT", "/api/site/timetable/pattern")).toHaveLength(0);

    fireEvent.click(within(warning).getByRole("button", { name: "انصراف" }));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/site/timetable/pattern")).toHaveLength(0);

    fireEvent.click(screen.getByRole("button", { name: "ذخیره الگو" }));
    fireEvent.click(await screen.findByRole("button", { name: "ذخیره با این حال" }));

    await screen.findByRole("button", { name: "تغییر الگو" });
    expect(api.requestsTo("PUT", "/api/site/timetable/pattern")).toHaveLength(1);
  });

  it("Day_ShowsEachSessionsStudents_AndTakesOneOutAfterAsking", async () => {
    const sara = {
      memberId: "0199a000-0000-7000-8000-000000000301",
      fullName: "سارا احمدی",
      phoneNumber: "+989121112233",
      moved: false,
    };
    const roster = (students: (typeof sara)[]) => ({
      date,
      sessions: [
        {
          start: "13:00:00",
          end: "14:00:00",
          capacity: 8,
          removed: false,
          started: false,
          students,
        },
        {
          start: "16:00:00",
          end: "17:00:00",
          capacity: null,
          removed: true,
          started: false,
          students: [
            { ...sara, memberId: "0199a000-0000-7000-8000-000000000302", fullName: "مینا رضایی" },
          ],
        },
      ],
    });
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () =>
        json(
          200,
          timetableWith(saturdayPattern, {
            [date]: {
              changedByHand: false,
              sessions: [{ start: "13:00:00", end: "14:00:00", capacity: 8, students: 1 }],
            },
          }),
        ),
      [`GET /api/site/timetable/days/${date}/students`]: () => json(200, roster([sara])),
      [`POST /api/site/timetable/days/${date}/students/remove`]: () => json(200, roster([])),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    const day = await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ });
    expect(day).toHaveTextContent("۱۳:۰۰ تا ۱۴:۰۰ · ۱ از ۸ نفر");
    fireEvent.click(day);

    const dialog = within(await screen.findByRole("dialog"));
    const first = within(await dialog.findByRole("region", { name: "۱۳:۰۰ تا ۱۴:۰۰" }));
    expect(first.getByRole("heading")).toHaveTextContent("۱ از ۸ نفر");
    expect(first.getByRole("link", { name: "۰۹۱۲ ۱۱۱ ۲۲۳۳" })).toHaveAttribute(
      "href",
      "tel:+989121112233",
    );
    const removed = within(dialog.getByRole("region", { name: "۱۶:۰۰ تا ۱۷:۰۰" }));
    expect(removed.getByText("لغو شد")).toBeInTheDocument();
    expect(removed.getByText("مینا رضایی")).toBeInTheDocument();

    fireEvent.click(first.getByRole("button", { name: "حذف از این روز" }));
    const confirm = await screen.findByRole("dialog", { name: "حذف از این روز" });
    expect(confirm).toHaveTextContent("سارا احمدی از سانس ۱۳:۰۰ تا ۱۴:۰۰ دوشنبه ۱۱ آبان");
    fireEvent.click(within(confirm).getByRole("button", { name: "حذف از این روز" }));

    expect(await first.findByText("شاگردی در این سانس نیست.")).toBeInTheDocument();
    const [sent] = api.requestsTo("POST", `/api/site/timetable/days/${date}/students/remove`);
    expect(await bodyOf(sent!)).toEqual({
      memberId: sara.memberId,
      start: "13:00:00",
      end: "14:00:00",
    });
  });

  it("Day_CloseItUnderStudents_WarnsFirst", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(saturdayPattern)),
      [`GET /api/site/timetable/days/${date}/students`]: noStudents(date),
      [`POST /api/site/timetable/days/${date}/check`]: () =>
        json(200, { students: 2, sessions: [touched.sessions[0]] }),
      [`PUT /api/site/timetable/days/${date}`]: () => json(200, timetableWith(saturdayPattern)),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ }));
    const dialog = within(await screen.findByRole("dialog"));
    fireEvent.click(dialog.getByRole("button", { name: "تغییر سانس‌های این روز" }));
    fireEvent.click(dialog.getByRole("button", { name: "تعطیل کردن این روز" }));

    expect(await dialog.findByRole("alert")).toHaveTextContent("این تغییر به ۲ شاگرد می‌رسد");
    expect(api.requestsTo("PUT", `/api/site/timetable/days/${date}`)).toHaveLength(0);
    fireEvent.click(dialog.getByRole("button", { name: "ذخیره با این حال" }));

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    const [sent] = api.requestsTo("PUT", `/api/site/timetable/days/${date}`);
    expect(await bodyOf(sent!)).toEqual({ sessions: [] });
  });
});
