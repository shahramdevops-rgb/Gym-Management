import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import { json, mockApi } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess, trainer } from "../test/renderSite";
import { monthOf, monthRange, shiftDay, shiftMonth } from "./labels";

const trainerOnly = siteAccess({ member: null, trainer });

const ali = {
  memberId: "0199a000-0000-7000-8000-000000000101",
  fullName: "علی محمدی",
  phoneNumber: "+989121234567",
};
const reza = {
  memberId: "0199a000-0000-7000-8000-000000000102",
  fullName: "رضا کریمی",
  phoneNumber: "+989121234568",
};
const neda = {
  memberId: "0199a000-0000-7000-8000-000000000103",
  fullName: "ندا حسینی",
  phoneNumber: "+989129876543",
};
const absentToday = {
  ...{ date: "2026-11-07", isToday: true, outside: [] },
  totals: { students: 1, present: 0, late: 0, outsideSession: 0, absent: 1 },
  sessions: [
    {
      start: "13:00:00",
      end: "14:00:00",
      isVip: false,
      removed: false,
      started: true,
      ended: true,
      students: [
        {
          ...neda,
          checkedOutAt: null,
          autoCheckedOut: false,
          minutesEarly: null,
          minutesLate: null,
          state: "Absent",
          checkedInAt: null,
        },
      ],
    },
  ],
};

const visit = {
  checkedOutAt: null,
  autoCheckedOut: false,
  minutesEarly: null,
  minutesLate: null,
};

const today = {
  date: "2026-11-07",
  isToday: true,
  totals: { students: 4, present: 2, late: 1, outsideSession: 1, absent: 0 },
  sessions: [
    {
      start: "13:00:00",
      end: "14:00:00",
      isVip: false,
      removed: false,
      started: true,
      ended: true,
      students: [
        {
          ...ali,
          ...visit,
          state: "Early",
          checkedInAt: "12:30:00",
          checkedOutAt: "14:40:00",
          minutesEarly: 30,
        },
        { ...reza, ...visit, state: "OutsideSession", checkedInAt: "16:00:00" },
        { ...neda, ...visit, state: "Late", checkedInAt: "13:25:00", minutesLate: 25 },
      ],
    },
    {
      start: "17:00:00",
      end: "18:00:00",
      isVip: true,
      removed: false,
      started: false,
      ended: false,
      students: [{ ...ali, ...visit, state: "NotYet", checkedInAt: null }],
    },
    {
      start: "15:00:00",
      end: "16:00:00",
      isVip: false,
      removed: true,
      started: true,
      ended: true,
      students: [{ ...reza, ...visit, state: "Cancelled", checkedInAt: null }],
    },
  ],
  outside: [
    {
      ...reza,
      visits: [{ checkedInAt: "16:00:00", checkedOutAt: null, autoCheckedOut: false }],
    },
  ],
};

const attendancePath = `/api/site/coaching/students/${ali.memberId}/attendance`;

const month = {
  memberId: ali.memberId,
  from: "2026-10-23",
  to: "2026-11-21",
  sessions: [
    {
      ...visit,
      date: "2026-10-24",
      start: "13:00:00",
      end: "14:00:00",
      trainerName: "سارا احمدی",
      isVip: false,
      state: "Late",
      checkedInAt: "13:20:00",
      autoCheckedOut: true,
      minutesLate: 20,
    },
    {
      // 15.7p: missed the 13:00 session, came at 19:30 that day.
      ...visit,
      date: "2026-10-27",
      start: "13:00:00",
      end: "14:00:00",
      trainerName: null,
      isVip: false,
      state: "OutsideSession",
      checkedInAt: "19:30:00",
      checkedOutAt: "21:00:00",
    },
    {
      ...visit,
      date: "2026-10-31",
      start: "13:00:00",
      end: "14:00:00",
      trainerName: null,
      isVip: false,
      state: "Leave",
      checkedInAt: null,
    },
  ],
  totals: {
    early: 0,
    onTime: 0,
    late: 1,
    outsideSession: 1,
    absent: 0,
    leave: 1,
    cancelled: 0,
    otherDayVisits: 1,
  },
  // 15.7p: a visit on a day with no session; the screen puts it among the sessions.
  otherDays: [
    {
      date: "2026-10-25",
      visit: { checkedInAt: "10:00:00", checkedOutAt: null, autoCheckedOut: true },
    },
  ],
};

/** The colour a count box is tinted with (15.7p): its token's name, or «muted» for a neutral one. */
function tintOf(element: HTMLElement): string | undefined {
  return (
    element.className.match(/bg-\[color-mix\(in_oklab,var\(--([a-z-]+)\)/)?.[1] ??
    element.className.match(/\bbg-(muted)\b/)?.[1]
  );
}

describe("the trainer's attendance (15.7b, 15.7d, 15.7m, 15.7p)", () => {
  it("DayPage_Today_ShowsEachStudentsStateAndCheckOut", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
    });
    renderSite("/today", { access: trainerOnly });

    const first = within(await screen.findByRole("list", { name: "شاگردان ۱۳:۰۰ تا ۱۴:۰۰" }));
    const [aliRow, rezaRow, nedaRow] = first.getAllByRole("listitem");
    expect(aliRow).toHaveTextContent("علی محمدی");
    expect(aliRow).toHaveTextContent("زودتر از موعد ۱۲:۳۰");
    expect(aliRow).toHaveTextContent("۳۰ دقیقه زودتر از موعد · خروج ۱۴:۴۰");
    expect(aliRow).not.toHaveTextContent("به‌موقع");
    // 15.7p: came that day at another hour, «خارج از سانس», not absent.
    expect(rezaRow).toHaveTextContent("خارج از سانس ۱۶:۰۰");
    expect(rezaRow).toHaveTextContent("داخل است");
    expect(rezaRow).not.toHaveTextContent("غایب");
    expect(within(rezaRow!).getByText(/خارج از سانس/)).toHaveAttribute(
      "data-variant",
      "outsideSession",
    );
    expect(nedaRow).toHaveTextContent("با تأخیر ۱۳:۲۵");
    expect(nedaRow).toHaveTextContent("۲۵ دقیقه تأخیر · داخل است");

    expect(screen.getByRole("listitem", { name: "۱۷:۰۰ تا ۱۸:۰۰" })).toHaveTextContent(
      "هنوز نیامده",
    );
    const removed = screen.getByRole("listitem", { name: "۱۵:۰۰ تا ۱۶:۰۰" });
    expect(removed).toHaveTextContent("لغو شد");
    expect(screen.getByRole("heading", { name: "حضور امروز", level: 1 })).toBeInTheDocument();
    expect(screen.getByText(/هر دقیقه به‌روز می‌شود/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "روز بعد" })).toBeDisabled();
  });

  it("DayPage_Today_ShowsTheCountsAndWhoCameOutsideTheSessions", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
    });
    renderSite("/today", { access: trainerOnly });

    const totals = within(await screen.findByRole("list", { name: "جمع امروز" }));
    expect(totals.getAllByRole("listitem").map((box) => box.textContent)).toEqual([
      "۴شاگردان",
      "۲حاضر",
      "۱با تأخیر",
      "۱خارج از سانس",
      "۰غایب",
    ]);

    const outside = within(screen.getByRole("list", { name: "آمدند، بدون سانس" }));
    const row = outside.getByRole("listitem");
    expect(row).toHaveTextContent("رضا کریمی");
    expect(row).toHaveTextContent("ورود ۱۶:۰۰ · داخل است");
  });

  it("DayPage_NoSessions_SaysSo", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
    });
    renderSite("/today", { access: trainerOnly });

    expect(await screen.findByText("امروز سانسی ندارید.")).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "جمع امروز" })).not.toBeInTheDocument();
  });

  it("DayPage_PreviousDay_AsksForThatDayAndDoesNotRefresh", async () => {
    const yesterday = shiftDay(gymToday(), -1);
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
      [`GET /api/site/coaching/days/${yesterday}`]: () =>
        json(200, { ...today, date: yesterday, isToday: false }),
    });
    renderSite("/today", { access: trainerOnly });
    await screen.findByRole("list", { name: "جمع امروز" });

    fireEvent.click(screen.getByRole("button", { name: "روز قبل" }));

    expect(await screen.findByRole("list", { name: "جمع روز" })).toBeInTheDocument();
    expect(api.requestsTo("GET", `/api/site/coaching/days/${yesterday}`)).toHaveLength(1);
    expect(screen.getByRole("heading", { name: "حضور", level: 1 })).toBeInTheDocument();
    expect(screen.queryByText(/هر دقیقه به‌روز می‌شود/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "روز بعد" })).toBeEnabled();
  });

  it("DayPage_DateInTheAddress_OpensThatDay", async () => {
    const earlier = shiftDay(gymToday(), -10);
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET /api/site/coaching/days/${earlier}`]: () =>
        json(200, {
          date: earlier,
          isToday: false,
          totals: { students: 0, present: 0, late: 0, outsideSession: 0, absent: 0 },
          sessions: [],
          outside: [],
        }),
    });
    renderSite(`/today?date=${earlier}`, { access: trainerOnly });

    expect(await screen.findByText("در این روز سانسی نداشتید.")).toBeInTheDocument();
  });

  it("DayPage_DateAfterToday_ShowsToday", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
    });
    renderSite(`/today?date=${shiftDay(gymToday(), 3)}`, { access: trainerOnly });

    expect(await screen.findByText("امروز سانسی ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/site/coaching/today")).toHaveLength(1);
  });

  it("Home_AbsentAfterAnEndedSession_ShowsTheCard", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, absentToday),
    });
    renderSite("/", { access: trainerOnly });

    const list = within(await screen.findByRole("list", { name: "غایبان امروز" }));
    const row = list.getByRole("listitem");
    expect(row).toHaveTextContent("ندا حسینی");
    expect(row).toHaveTextContent("۱۳:۰۰ تا ۱۴:۰۰");
  });

  it("Home_OutsideSession_IsNotInTheAbsentCard", async () => {
    // 15.7p (the developer, 1405/08/29): came that day at another hour, so not absent.
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
    });
    renderSite("/", { access: trainerOnly });

    await screen.findByRole("list", { name: "جمع امروز" });
    expect(screen.queryByRole("heading", { name: "غایبان امروز" })).not.toBeInTheDocument();
  });

  it("Home_DayWithSessions_ShowsTodaysCounts", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
    });
    renderSite("/", { access: trainerOnly });

    const totals = within(await screen.findByRole("list", { name: "جمع امروز" }));
    expect(totals.getAllByRole("listitem")[0]).toHaveTextContent("۴شاگردان");
    expect(screen.getByRole("heading", { name: "امروز", level: 2 })).toBeInTheDocument();
  });

  it("Home_NoSessionsAndNoOneAbsent_ShowsNeitherCard", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
    });
    renderSite("/", { access: trainerOnly });

    await waitFor(() => expect(api.requestsTo("GET", "/api/site/coaching/today")).toHaveLength(1));
    expect(screen.queryByRole("heading", { name: "غایبان امروز" })).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "امروز" })).not.toBeInTheDocument();
  });

  it("Students_TrainersStudent_LeadsToTheirAttendance", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/students": () =>
        json(200, { requests: [], students: [{ ...ali, since: "2026-10-01", isActive: true }] }),
      [`GET ${attendancePath}`]: () => json(200, month),
    });
    renderSite("/students", { access: trainerOnly });

    fireEvent.click(await screen.findByRole("link", { name: "حضور علی محمدی" }));

    expect(await screen.findByRole("heading", { name: "حضور", level: 1 })).toBeInTheDocument();
  });

  it("AttendancePage_Month_ShowsEveryDayTheyCameInDateAndTimeOrder", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/students": () =>
        json(200, { requests: [], students: [{ ...ali, since: "2026-10-01", isActive: true }] }),
      [`GET ${attendancePath}`]: () => json(200, month),
    });
    renderSite(`/students/${ali.memberId}/attendance`, { access: trainerOnly });

    const rows = within(await screen.findByRole("list", { name: "حضور ماه" }));
    const [late, otherDay, outsideSession, leave] = rows.getAllByRole("listitem");
    expect(late).toHaveTextContent("با تأخیر ۱۳:۲۰");
    expect(late).toHaveTextContent("۲۰ دقیقه تأخیر · خروج ثبت نشد");
    expect(late).toHaveTextContent("با سارا احمدی");
    // 15.7p: a day with no session is a row of its own, purple, among the sessions.
    expect(otherDay).toHaveTextContent("ورود ۱۰:۰۰ · خروج ثبت نشد");
    expect(within(otherDay!).getByText("روز دیگر")).toHaveAttribute("data-variant", "otherDay");
    // A session they missed but came that day at another hour: «خارج از سانس», dark blue.
    expect(outsideSession).toHaveTextContent("خارج از سانس ۱۹:۳۰");
    expect(outsideSession).toHaveTextContent("خروج ۲۱:۰۰");
    expect(within(outsideSession!).getByText(/خارج از سانس/)).toHaveAttribute(
      "data-variant",
      "outsideSession",
    );
    expect(leave).toHaveTextContent("مرخصی");
    expect(leave).not.toHaveTextContent("با ");
    expect(leave).not.toHaveTextContent("داخل است");
    // 15.7d's list at the foot of the page is gone.
    expect(screen.queryByRole("heading", { name: "خارج از سانس" })).not.toBeInTheDocument();
    expect(await screen.findByText("علی محمدی")).toBeInTheDocument();

    const thisMonth = monthRange(monthOf(gymToday()));
    const asked = new URL(api.requestsTo("GET", attendancePath)[0]!.url).searchParams;
    expect([asked.get("from"), asked.get("to")]).toEqual([thisMonth.from, thisMonth.to]);
    expect(screen.getByRole("button", { name: "ماه بعد" })).toBeDisabled();
  });

  it("AttendancePage_Month_EightBoxesFourARowEachInItsStatesColour", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${attendancePath}`]: () => json(200, month),
    });
    renderSite(`/students/${ali.memberId}/attendance`, { access: trainerOnly });

    // The developer, 1405/08/29: the four a student who came is in, then the rest.
    const totals = await screen.findByRole("list", { name: "جمع ماه" });
    const boxes = within(totals).getAllByRole("listitem");
    expect(boxes.map((box) => box.textContent)).toEqual([
      "۰زودتر از موعد",
      "۰به‌موقع",
      "۱با تأخیر",
      "۱خارج از سانس",
      "۰غایب",
      "۱روز دیگر",
      "۱مرخصی",
      "۰لغو شد",
    ]);
    expect(totals).toHaveClass("grid-cols-4");
    // Each box in its badge's colour: «خارج از سانس» dark blue, «روز دیگر» purple, «غایب» red.
    expect(boxes.map(tintOf)).toEqual([
      "early",
      "success",
      "warning",
      "outside-session",
      "destructive",
      "other-day",
      "muted",
      "muted",
    ]);
  });

  it("AttendancePage_NoOtherDay_ShowsOnlyTheSessions", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${attendancePath}`]: () =>
        json(200, {
          ...month,
          otherDays: [],
          totals: { ...month.totals, otherDayVisits: 0 },
        }),
    });
    renderSite(`/students/${ali.memberId}/attendance`, { access: trainerOnly });

    const rows = within(await screen.findByRole("list", { name: "حضور ماه" }));
    expect(rows.getAllByRole("listitem")).toHaveLength(3);
    expect(screen.queryByText("روز دیگر", { selector: "[data-slot='badge']" })).toBeNull();
  });

  it("AttendancePage_OnlyOtherDaysNoSession_ListsTheVisits", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${attendancePath}`]: () => json(200, { ...month, sessions: [] }),
    });
    renderSite(`/students/${ali.memberId}/attendance`, { access: trainerOnly });

    const rows = within(await screen.findByRole("list", { name: "حضور ماه" }));
    expect(rows.getAllByRole("listitem")).toHaveLength(1);
    expect(screen.queryByText("در این ماه سانسی نداشته است.")).not.toBeInTheDocument();
  });

  it("AttendancePage_PreviousMonth_AsksForItsRange", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${attendancePath}`]: () => json(200, month),
    });
    renderSite(`/students/${ali.memberId}/attendance`, { access: trainerOnly });
    await screen.findByRole("list", { name: "حضور ماه" });

    fireEvent.click(screen.getByRole("button", { name: "ماه قبل" }));

    const before = monthRange(shiftMonth(monthOf(gymToday()), -1));
    await waitFor(() => expect(api.requestsTo("GET", attendancePath)).toHaveLength(2));
    const asked = new URL(api.requestsTo("GET", attendancePath)[1]!.url).searchParams;
    expect([asked.get("from"), asked.get("to")]).toEqual([before.from, before.to]);
    expect(screen.getByRole("button", { name: "ماه بعد" })).toBeEnabled();
  });

  // ---- 15.7f: the counts open their students ----

  function filterApi() {
    return mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
      [`GET /api/site/coaching/days/${shiftDay(gymToday(), -1)}`]: () =>
        json(200, { ...today, date: shiftDay(gymToday(), -1), isToday: false }),
    });
  }

  const box = (name: RegExp) =>
    within(screen.getByRole("list", { name: "جمع امروز" })).getByRole("button", { name });

  it("DayPage_Boxes_EachInItsStatesColourAndFilledWhenPressed", async () => {
    filterApi();
    renderSite("/today", { access: trainerOnly });
    const totals = await screen.findByRole("list", { name: "جمع امروز" });

    // 15.7p, the developer: «حاضر» green, «با تأخیر» amber, «خارج از سانس» dark blue, «غایب» red.
    expect(within(totals).getAllByRole("button").map(tintOf)).toEqual([
      "muted",
      "success",
      "warning",
      "outside-session",
      "destructive",
    ]);

    fireEvent.click(box(/خارج از سانس/));

    // Pressed: filled with its colour, as its badge is, and only that student shows.
    expect(box(/خارج از سانس/)).toHaveClass("bg-outside-session");
    expect(tintOf(box(/خارج از سانس/))).toBeUndefined();
    const sessions = within(screen.getByRole("list", { name: "سانس‌های روز" }));
    expect(sessions.getByText("رضا کریمی")).toBeInTheDocument();
    expect(sessions.queryByText("علی محمدی")).not.toBeInTheDocument();
  });

  it("DayPage_NameOfAStudentNow_OpensTheirProfile", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
      // Reza is no longer the trainer's student: his name stays text (15.7p).
      "GET /api/site/coaching/students": () =>
        json(200, {
          requests: [],
          periodSoonDays: 7,
          students: [
            { ...ali, since: "2026-10-01", isActive: true, periodEndsOn: null },
            { ...neda, since: "2026-10-01", isActive: true, periodEndsOn: null },
          ],
        }),
    });
    renderSite("/today", { access: trainerOnly });

    const first = within(await screen.findByRole("list", { name: "شاگردان ۱۳:۰۰ تا ۱۴:۰۰" }));
    expect(await first.findByRole("link", { name: "علی محمدی" })).toHaveAttribute(
      "href",
      `/students/${ali.memberId}/profile`,
    );
    expect(first.queryByRole("link", { name: "رضا کریمی" })).not.toBeInTheDocument();
    expect(first.getByText("رضا کریمی")).toBeInTheDocument();
    const outside = within(screen.getByRole("list", { name: "آمدند، بدون سانس" }));
    expect(outside.queryByRole("link", { name: "رضا کریمی" })).not.toBeInTheDocument();
  });

  it("DayPage_PressABox_ShowsOnlyItsStudentsAndAgainShowsAll", async () => {
    filterApi();
    renderSite("/today", { access: trainerOnly });
    await screen.findByRole("list", { name: "جمع امروز" });

    fireEvent.click(box(/با تأخیر/));

    expect(box(/با تأخیر/)).toHaveAttribute("aria-pressed", "true");
    const sessions = within(screen.getByRole("list", { name: "سانس‌های روز" }));
    expect(sessions.getAllByRole("listitem", { name: /تا/ })).toHaveLength(1);
    expect(sessions.getByText("ندا حسینی")).toBeInTheDocument();
    expect(sessions.queryByText("علی محمدی")).not.toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "آمدند، بدون سانس" })).not.toBeInTheDocument();

    fireEvent.click(box(/با تأخیر/));

    expect(box(/با تأخیر/)).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("listitem", { name: "۱۵:۰۰ تا ۱۶:۰۰" })).toBeInTheDocument();
    expect(screen.getByRole("list", { name: "آمدند، بدون سانس" })).toBeInTheDocument();
  });

  it("DayPage_PressPresent_KeepsTheEarlyAndTheLate", async () => {
    filterApi();
    renderSite("/today", { access: trainerOnly });
    await screen.findByRole("list", { name: "جمع امروز" });

    fireEvent.click(box(/حاضر/));

    // «زودتر از موعد» came to the session, so it is present (15.7m); came after is not.
    const sessions = within(screen.getByRole("list", { name: "سانس‌های روز" }));
    expect(sessions.getByText("علی محمدی")).toBeInTheDocument();
    expect(sessions.getByText("ندا حسینی")).toBeInTheDocument();
    expect(sessions.queryByText("رضا کریمی")).not.toBeInTheDocument();
  });

  it("DayPage_PressStudents_LeavesOutLeaveAndCancelled", async () => {
    filterApi();
    renderSite("/today", { access: trainerOnly });
    await screen.findByRole("list", { name: "جمع امروز" });

    fireEvent.click(box(/شاگردان/));

    // The cancelled session's only student is in no count, so the session goes.
    expect(screen.queryByRole("listitem", { name: "۱۵:۰۰ تا ۱۶:۰۰" })).not.toBeInTheDocument();
    expect(screen.getByRole("listitem", { name: "۱۷:۰۰ تا ۱۸:۰۰" })).toHaveTextContent(
      "هنوز نیامده",
    );
  });

  it("DayPage_BoxWithNoOne_SaysSo", async () => {
    // A day with no one late (15.7p made Reza absent, so «غایب» has someone now).
    const noOneLate = {
      ...today,
      sessions: today.sessions.map((session) => ({
        ...session,
        students: session.students.filter((student) => student.state !== "Late"),
      })),
    };
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, noOneLate),
    });
    renderSite("/today", { access: trainerOnly });
    await screen.findByRole("list", { name: "جمع امروز" });

    fireEvent.click(box(/با تأخیر/));

    expect(screen.getByText("کسی در این دسته نیست.")).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "سانس‌های روز" })).not.toBeInTheDocument();
  });

  it("DayPage_AnotherDay_KeepsTheFilter", async () => {
    const api = filterApi();
    renderSite("/today?show=present", { access: trainerOnly });
    await screen.findByRole("list", { name: "جمع امروز" });

    fireEvent.click(screen.getByRole("button", { name: "روز قبل" }));

    const totals = within(await screen.findByRole("list", { name: "جمع روز" }));
    expect(totals.getByRole("button", { name: /حاضر/ })).toHaveAttribute("aria-pressed", "true");
    expect(
      api.requestsTo("GET", `/api/site/coaching/days/${shiftDay(gymToday(), -1)}`),
    ).toHaveLength(1);
  });

  it("Home_CountBox_OpensTheDayPageOnItsStudents", async () => {
    filterApi();
    renderSite("/", { access: trainerOnly });

    const totals = within(await screen.findByRole("list", { name: "جمع امروز" }));
    fireEvent.click(totals.getByRole("link", { name: /حاضر/ }));

    expect(
      await screen.findByRole("heading", { name: "حضور امروز", level: 1 }),
    ).toBeInTheDocument();
    expect(box(/حاضر/)).toHaveAttribute("aria-pressed", "true");
    const sessions = within(screen.getByRole("list", { name: "سانس‌های روز" }));
    expect(sessions.getByText("علی محمدی")).toBeInTheDocument();
    expect(sessions.getByText("ندا حسینی")).toBeInTheDocument();
    expect(sessions.queryByText("رضا کریمی")).not.toBeInTheDocument();
  });
});
