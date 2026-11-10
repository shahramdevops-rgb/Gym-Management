import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi } from "@/test/mockApi";

import { emptyHome, emptyTimetable, renderSite, siteAccess, trainer } from "../test/renderSite";

// A Saturday: 1405/08/09.
const today = "2026-10-31";
const tomorrow = "2026-11-01";
// A Monday: 1405/08/11.
const date = "2026-11-02";

const trainerOnly = { member: null, trainer };

async function bodyOf(request: Request) {
  return (await request.json()) as unknown;
}

/** The timetable with nothing but `pattern`, and `sessions` on `date`. */
function timetableWith(pattern: unknown, sessions: unknown[] = []) {
  const empty = emptyTimetable(today);

  return {
    ...empty,
    pattern,
    days: empty.days.map((day) =>
      day.date === date ? { ...day, changedByHand: false, sessions } : day,
    ),
  };
}

/** «شاگردان سانس» of `date`: one 13:00 session with no student and these notes. */
function roster(extra: Record<string, unknown> = {}) {
  return {
    date,
    sessions: [
      {
        start: "13:00:00",
        end: "14:00:00",
        capacity: 8,
        isVip: false,
        note: null,
        dateNote: null,
        removed: false,
        started: false,
        ended: false,
        students: [],
        ...extra,
      },
    ],
  };
}

const mondaySession = { start: "13:00:00", end: "14:00:00", capacity: 8, students: 0 };

describe("the trainer's notes on sessions (15.7e)", () => {
  it("BuildPattern_ASessionWithAStandingNote_SendsItAndShowsIt", async () => {
    const noted = {
      day: "Saturday",
      start: "08:00:00",
      end: "09:00:00",
      capacity: 10,
      isVip: false,
      note: "مخصوص مبتدی‌ها",
    };
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(null)),
      "POST /api/site/timetable/pattern/check": () =>
        json(200, { students: 0, sessions: [], toReschedule: [] }),
      "PUT /api/site/timetable/pattern": () =>
        json(200, timetableWith({ startsOn: tomorrow, sessions: [noted] })),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "ساختن الگو" }));
    const saturday = within(screen.getByRole("region", { name: "شنبه" }));
    fireEvent.click(saturday.getByRole("button", { name: "افزودن سانس به شنبه" }));
    fireEvent.change(saturday.getByRole("textbox", { name: "یادداشت ثابت سانس ۱" }), {
      target: { value: "  مخصوص مبتدی‌ها " },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره الگو" }));

    const shown = await screen.findByRole("list", { name: "الگوی هفتگی" });
    expect(within(shown).getAllByRole("listitem")[0]).toHaveTextContent("مخصوص مبتدی‌ها");
    const [sent] = api.requestsTo("PUT", "/api/site/timetable/pattern");
    expect(await bodyOf(sent!)).toEqual({ startsOn: tomorrow, sessions: [noted] });
  });

  it("EditPattern_ANotedSession_KeepsTheNoteInTheBox", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () =>
        json(
          200,
          timetableWith({
            startsOn: tomorrow,
            sessions: [
              {
                day: "Saturday",
                start: "13:00:00",
                end: "14:00:00",
                capacity: 8,
                isVip: false,
                note: "فقط بانوان",
              },
            ],
          }),
        ),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر الگو" }));

    const saturday = within(screen.getByRole("region", { name: "شنبه" }));
    expect(saturday.getByRole("textbox", { name: "یادداشت ثابت سانس ۱" })).toHaveValue(
      "فقط بانوان",
    );
  });

  it("Days_ASessionWithANote_IsMarked", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () =>
        json(200, timetableWith(null, [{ ...mondaySession, note: null, dateNote: "تمرین پا" }])),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    const day = await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ });
    expect(within(day).getByLabelText("یادداشت دارد")).toBeInTheDocument();
  });

  it("Day_WriteADateNote_SendsItAndShowsIt", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(null, [mondaySession])),
      [`GET /api/site/timetable/days/${date}/students`]: () => json(200, roster()),
      [`PUT /api/site/timetable/days/${date}/note`]: () =>
        json(200, roster({ dateNote: "امروز تمرین پا است." })),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ }));
    const dialog = within(await screen.findByRole("dialog"));
    const session = within(await dialog.findByRole("region", { name: "۱۳:۰۰ تا ۱۴:۰۰" }));
    fireEvent.click(session.getByRole("button", { name: "یادداشت این روز" }));
    const save = session.getByRole("button", { name: "ذخیره" });
    expect(save).toBeDisabled();
    fireEvent.change(session.getByRole("textbox", { name: "یادداشت این روز" }), {
      target: { value: "امروز تمرین پا است." },
    });
    fireEvent.click(save);

    await vi.waitFor(() =>
      expect(api.requestsTo("PUT", `/api/site/timetable/days/${date}/note`)).toHaveLength(1),
    );
    expect(await session.findByText("امروز تمرین پا است.")).toBeInTheDocument();
    expect(session.getByRole("button", { name: "ویرایش یادداشت ۱۳:۰۰ تا ۱۴:۰۰" })).toBeVisible();
    const [sent] = api.requestsTo("PUT", `/api/site/timetable/days/${date}/note`);
    expect(await bodyOf(sent!)).toEqual({
      start: "13:00:00",
      end: "14:00:00",
      text: "امروز تمرین پا است.",
    });
  });

  it("Day_RemoveADateNote_AsksFirst", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(null, [mondaySession])),
      [`GET /api/site/timetable/days/${date}/students`]: () =>
        json(200, roster({ note: "مخصوص مبتدی‌ها", dateNote: "تمرین پا" })),
      [`POST /api/site/timetable/days/${date}/note/remove`]: () =>
        json(200, roster({ note: "مخصوص مبتدی‌ها" })),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ }));
    const dialog = within(await screen.findByRole("dialog"));
    const session = within(await dialog.findByRole("region", { name: "۱۳:۰۰ تا ۱۴:۰۰" }));
    expect(session.getByText("مخصوص مبتدی‌ها")).toBeInTheDocument();
    fireEvent.click(session.getByRole("button", { name: "پاک کردن یادداشت" }));
    const confirm = await screen.findByRole("dialog", { name: "پاک کردن یادداشت" });
    expect(confirm).toHaveTextContent("یادداشت سانس ۱۳:۰۰ تا ۱۴:۰۰ دوشنبه ۱۱ آبان پاک می‌شود");
    fireEvent.click(within(confirm).getByRole("button", { name: "پاک کردن یادداشت" }));

    expect(await session.findByRole("button", { name: "یادداشت این روز" })).toBeInTheDocument();
    expect(session.queryByText("تمرین پا")).not.toBeInTheDocument();
    expect(session.getByText("مخصوص مبتدی‌ها")).toBeInTheDocument();
    const [sent] = api.requestsTo("POST", `/api/site/timetable/days/${date}/note/remove`);
    expect(await bodyOf(sent!)).toEqual({ start: "13:00:00", end: "14:00:00" });
  });

  it("Day_ASessionThatHasEnded_ShowsTheNoteWithoutChangingIt", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetableWith(null, [mondaySession])),
      [`GET /api/site/timetable/days/${date}/students`]: () =>
        json(200, roster({ dateNote: "تمرین پا", started: true, ended: true })),
    });
    renderSite("/timetable", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: /دوشنبه ۱۱ آبان/ }));
    const dialog = within(await screen.findByRole("dialog"));
    const session = within(await dialog.findByRole("region", { name: "۱۳:۰۰ تا ۱۴:۰۰" }));
    expect(session.getByText("تمرین پا")).toBeInTheDocument();
    expect(session.queryByRole("button", { name: /یادداشت/ })).not.toBeInTheDocument();
  });

  it("DayPage_ASessionWithNotes_ShowsBoth", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess(trainerOnly).me),
      "GET /api/site/coaching/today": () =>
        json(200, {
          date: today,
          isToday: true,
          totals: { students: 0, present: 0, late: 0, cameAfter: 0, absent: 0 },
          sessions: [
            {
              start: "13:00:00",
              end: "14:00:00",
              isVip: false,
              note: "مخصوص مبتدی‌ها",
              dateNote: "تمرین پا",
              removed: false,
              started: false,
              ended: false,
              students: [],
            },
          ],
          outside: [],
        }),
    });
    renderSite("/today", { access: siteAccess(trainerOnly) });

    const session = await screen.findByRole("listitem", { name: "۱۳:۰۰ تا ۱۴:۰۰" });
    expect(session).toHaveTextContent("یادداشت ثابت: مخصوص مبتدی‌ها");
    expect(session).toHaveTextContent("یادداشت این روز: تمرین پا");
  });
});

describe("the student sees the notes (15.7e)", () => {
  const place = (day: string, extra: Record<string, unknown> = {}) => ({
    date: day,
    start: "13:00:00",
    end: "14:00:00",
    isVip: false,
    moved: false,
    removed: false,
    started: false,
    left: false,
    note: "مخصوص مبتدی‌ها",
    dateNote: null,
    ...extra,
  });
  const mine = (sessions: unknown[]) => ({
    today,
    trainerName: trainer.fullName,
    schedule: {
      startsOn: "2026-10-24",
      endsOn: "2026-12-18",
      slots: [{ day: "Monday", start: "13:00:00", end: "14:00:00" }],
    },
    sessions,
    openings: [],
  });

  it("MySessions_NotesUnderTheirDate_OnLeaveToo", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () =>
        json(
          200,
          mine([place(date, { dateNote: "امروز تمرین پا است.", left: true }), place("2026-11-04")]),
        ),
    });
    renderSite("/sessions", { access: siteAccess() });

    const list = within(await screen.findByRole("list", { name: "سانس‌های پیش رو" }));
    const [monday, nextMonday] = list.getAllByRole("listitem");
    expect(monday).toHaveTextContent("مرخصی");
    expect(monday).toHaveTextContent("یادداشت این روز: امروز تمرین پا است.");
    expect(monday).toHaveTextContent("یادداشت ثابت: مخصوص مبتدی‌ها");
    expect(nextMonday).toHaveTextContent("یادداشت ثابت: مخصوص مبتدی‌ها");
    expect(nextMonday).not.toHaveTextContent("یادداشت این روز");
  });

  it("Home_TheNextSession_ShowsItsDateNote", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions": () =>
        json(200, mine([place(date, { note: null, dateNote: "حوله بیاورید" })])),
    });
    renderSite("/", { access: siteAccess() });

    const next = within(await screen.findByRole("list", { name: "سانس‌های بعدی" }));
    expect(next.getByRole("listitem")).toHaveTextContent("حوله بیاورید");
  });
});
