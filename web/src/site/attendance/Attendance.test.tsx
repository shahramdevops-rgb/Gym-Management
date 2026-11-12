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
  totals: { students: 1, present: 0, late: 0, cameAfter: 0, absent: 1 },
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
  totals: { students: 4, present: 2, late: 1, cameAfter: 1, absent: 0 },
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
          state: "OnTime",
          checkedInAt: "12:50:00",
          checkedOutAt: "14:40:00",
          minutesEarly: 10,
        },
        { ...reza, ...visit, state: "CameAfter", checkedInAt: "16:00:00" },
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
  totals: { onTime: 0, late: 1, cameAfter: 0, absent: 0, leave: 1, cancelled: 0 },
  outside: [
    {
      date: "2026-10-25",
      visit: { checkedInAt: "18:00:00", checkedOutAt: "19:30:00", autoCheckedOut: false },
    },
  ],
};

describe("the trainer's attendance (15.7b, 15.7d)", () => {
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
    expect(aliRow).toHaveTextContent("به‌موقع ۱۲:۵۰");
    expect(aliRow).toHaveTextContent("۱۰ دقیقه زودتر آمد · خروج ۱۴:۴۰");
    expect(rezaRow).toHaveTextContent("بعد از سانس آمد ۱۶:۰۰");
    expect(rezaRow).toHaveTextContent("داخل است");
    expect(rezaRow).not.toHaveTextContent("غایب");
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
      "۱بعد از سانس",
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
          totals: { students: 0, present: 0, late: 0, cameAfter: 0, absent: 0 },
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

  it("Home_CameAfterTheSession_IsNotInTheAbsentCard", async () => {
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

  it("AttendancePage_Month_ShowsTotalsSessionsAndVisitsOutside", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/students": () =>
        json(200, { requests: [], students: [{ ...ali, since: "2026-10-01", isActive: true }] }),
      [`GET ${attendancePath}`]: () => json(200, month),
    });
    renderSite(`/students/${ali.memberId}/attendance`, { access: trainerOnly });

    const sessions = within(await screen.findByRole("list", { name: "سانس‌های ماه" }));
    const [late, leave] = sessions.getAllByRole("listitem");
    expect(late).toHaveTextContent("با تأخیر ۱۳:۲۰");
    expect(late).toHaveTextContent("۲۰ دقیقه تأخیر · خروج ثبت نشد");
    expect(late).toHaveTextContent("با سارا احمدی");
    expect(leave).toHaveTextContent("مرخصی");
    expect(leave).not.toHaveTextContent("با ");
    expect(leave).not.toHaveTextContent("داخل است");

    const totals = within(screen.getByRole("list", { name: "جمع ماه" }));
    expect(totals.getAllByRole("listitem").map((box) => box.textContent)).toEqual([
      "۰به‌موقع",
      "۱با تأخیر",
      "۰بعد از سانس",
      "۰غایب",
      "۱مرخصی",
      "۰لغو شد",
    ]);

    const outside = within(screen.getByRole("list", { name: "خارج از سانس" }));
    expect(outside.getByRole("listitem")).toHaveTextContent("ورود ۱۸:۰۰ · خروج ۱۹:۳۰");
    expect(await screen.findByText("علی محمدی")).toBeInTheDocument();

    const thisMonth = monthRange(monthOf(gymToday()));
    const asked = new URL(api.requestsTo("GET", attendancePath)[0]!.url).searchParams;
    expect([asked.get("from"), asked.get("to")]).toEqual([thisMonth.from, thisMonth.to]);
    expect(screen.getByRole("button", { name: "ماه بعد" })).toBeDisabled();
  });

  it("AttendancePage_NoVisitOutside_ShowsNoSuchList", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${attendancePath}`]: () => json(200, { ...month, outside: [] }),
    });
    renderSite(`/students/${ali.memberId}/attendance`, { access: trainerOnly });

    await screen.findByRole("list", { name: "سانس‌های ماه" });
    expect(screen.queryByRole("list", { name: "خارج از سانس" })).not.toBeInTheDocument();
  });

  it("AttendancePage_PreviousMonth_AsksForItsRange", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${attendancePath}`]: () => json(200, month),
    });
    renderSite(`/students/${ali.memberId}/attendance`, { access: trainerOnly });
    await screen.findByRole("list", { name: "سانس‌های ماه" });

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
    filterApi();
    renderSite("/today", { access: trainerOnly });
    await screen.findByRole("list", { name: "جمع امروز" });

    fireEvent.click(box(/غایب/));

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
