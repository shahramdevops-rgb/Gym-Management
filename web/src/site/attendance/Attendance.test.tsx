import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import { json, mockApi } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess, trainer } from "../test/renderSite";
import { monthOf, monthRange, shiftMonth } from "./labels";

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

const today = {
  date: "2026-11-07",
  sessions: [
    {
      start: "13:00:00",
      end: "14:00:00",
      isVip: false,
      removed: false,
      started: true,
      ended: true,
      students: [
        { ...ali, state: "OnTime", checkedInAt: "13:10:00", cameAfter: false },
        { ...reza, state: "Absent", checkedInAt: "16:00:00", cameAfter: true },
      ],
    },
    {
      start: "17:00:00",
      end: "18:00:00",
      isVip: true,
      removed: false,
      started: false,
      ended: false,
      students: [{ ...ali, state: "NotYet", checkedInAt: null, cameAfter: false }],
    },
    {
      start: "15:00:00",
      end: "16:00:00",
      isVip: false,
      removed: true,
      started: true,
      ended: true,
      students: [{ ...reza, state: "Cancelled", checkedInAt: null, cameAfter: false }],
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
      date: "2026-10-24",
      start: "13:00:00",
      end: "14:00:00",
      trainerName: "سارا احمدی",
      isVip: false,
      state: "Late",
      checkedInAt: "13:20:00",
      cameAfter: false,
    },
    {
      date: "2026-10-31",
      start: "13:00:00",
      end: "14:00:00",
      trainerName: null,
      isVip: false,
      state: "Leave",
      checkedInAt: null,
      cameAfter: false,
    },
  ],
  totals: { onTime: 0, late: 1, absent: 0, leave: 1, cancelled: 0 },
};

describe("the trainer's attendance (15.7b)", () => {
  it("TodayPage_Sessions_ShowsEachStudentsState", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
    });
    renderSite("/today", { access: trainerOnly });

    const first = within(await screen.findByRole("list", { name: "شاگردان ۱۳:۰۰ تا ۱۴:۰۰" }));
    const [aliRow, rezaRow] = first.getAllByRole("listitem");
    expect(aliRow).toHaveTextContent("علی محمدی");
    expect(aliRow).toHaveTextContent("به‌موقع ۱۳:۱۰");
    expect(rezaRow).toHaveTextContent("غایب");
    expect(rezaRow).toHaveTextContent("بعد از سانس آمد ۱۶:۰۰");

    expect(screen.getByRole("listitem", { name: "۱۷:۰۰ تا ۱۸:۰۰" })).toHaveTextContent(
      "هنوز نیامده",
    );
    const removed = screen.getByRole("listitem", { name: "۱۵:۰۰ تا ۱۶:۰۰" });
    expect(removed).toHaveTextContent("لغو شد");
    expect(screen.getByText(/هر دقیقه به‌روز می‌شود/)).toBeInTheDocument();
  });

  it("TodayPage_NoSessions_SaysSo", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
    });
    renderSite("/today", { access: trainerOnly });

    expect(await screen.findByText("امروز سانسی ندارید.")).toBeInTheDocument();
  });

  it("Home_AbsentAfterAnEndedSession_ShowsTheCard", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () => json(200, today),
    });
    renderSite("/", { access: trainerOnly });

    const list = within(await screen.findByRole("list", { name: "غایبان امروز" }));
    const row = list.getByRole("listitem");
    expect(row).toHaveTextContent("رضا کریمی");
    expect(row).toHaveTextContent("۱۳:۰۰ تا ۱۴:۰۰ · بعد از سانس آمد ۱۶:۰۰");
  });

  it("Home_NoOneAbsent_ShowsNoCard", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
    });
    renderSite("/", { access: trainerOnly });

    await waitFor(() => expect(api.requestsTo("GET", "/api/site/coaching/today")).toHaveLength(1));
    expect(screen.queryByRole("heading", { name: "غایبان امروز" })).not.toBeInTheDocument();
  });

  it("Home_TrainersStudent_LeadsToTheirAttendance", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/students": () =>
        json(200, { requests: [], students: [{ ...ali, since: "2026-10-01", isActive: true }] }),
      [`GET ${attendancePath}`]: () => json(200, month),
    });
    renderSite("/", { access: trainerOnly });

    fireEvent.click(await screen.findByRole("link", { name: "حضور علی محمدی" }));

    expect(await screen.findByRole("heading", { name: "حضور", level: 1 })).toBeInTheDocument();
  });

  it("AttendancePage_Month_ShowsTotalsAndSessions", async () => {
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
    expect(late).toHaveTextContent("با سارا احمدی");
    expect(leave).toHaveTextContent("مرخصی");
    expect(leave).not.toHaveTextContent("با ");

    const totals = within(screen.getByRole("list", { name: "جمع ماه" }));
    expect(totals.getAllByRole("listitem").map((box) => box.textContent)).toEqual([
      "۰به‌موقع",
      "۱با تأخیر",
      "۰غایب",
      "۱مرخصی",
      "۰لغو شد",
    ]);
    expect(await screen.findByText("علی محمدی")).toBeInTheDocument();

    const thisMonth = monthRange(monthOf(gymToday()));
    const asked = new URL(api.requestsTo("GET", attendancePath)[0]!.url).searchParams;
    expect([asked.get("from"), asked.get("to")]).toEqual([thisMonth.from, thisMonth.to]);
    expect(screen.getByRole("button", { name: "ماه بعد" })).toBeDisabled();
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
});
