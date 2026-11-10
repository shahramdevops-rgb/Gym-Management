import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import { json, mockApi } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess } from "../test/renderSite";
import { monthOf, monthRange, shiftMonth } from "./labels";

/*
 * «حضور من» (BUSINESS_RULES.md §15 *The member's own attendance*, roadmap 15.7j): the member's own
 * month, read only, as their trainer sees it but without «خارج از سانس».
 */

const memberOnly = siteAccess();
const path = "/api/site/me/attendance";

const visit = {
  checkedOutAt: null,
  autoCheckedOut: false,
  minutesEarly: null,
  minutesLate: null,
  hadLeave: false,
};

const month = {
  from: "2026-10-23",
  to: "2026-11-21",
  sessions: [
    {
      ...visit,
      date: "2026-10-25",
      start: "13:00:00",
      end: "14:00:00",
      trainerName: "سارا احمدی",
      isVip: false,
      state: "OnTime",
      checkedInAt: "12:50:00",
      minutesEarly: 10,
    },
    {
      ...visit,
      date: "2026-11-01",
      start: "13:00:00",
      end: "14:00:00",
      trainerName: null,
      isVip: false,
      state: "Late",
      checkedInAt: "13:25:00",
      minutesLate: 25,
      hadLeave: true,
    },
    {
      ...visit,
      date: "2026-11-03",
      start: "13:00:00",
      end: "14:00:00",
      trainerName: null,
      isVip: false,
      state: "Absent",
      checkedInAt: null,
    },
  ],
  totals: { onTime: 1, late: 1, cameAfter: 0, absent: 1, leave: 0, cancelled: 0 },
};

describe("the member's «حضور من»", () => {
  it("MySessions_HasTheWayIn", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, memberOnly.me),
    });
    renderSite("/sessions", { access: memberOnly });

    expect(await screen.findByRole("link", { name: "حضور من" })).toHaveAttribute(
      "href",
      "/my-attendance",
    );
  });

  it("MyAttendance_Month_ShowsTotalsAndEachSessionsState", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, memberOnly.me),
      [`GET ${path}`]: () => json(200, month),
    });
    renderSite("/my-attendance", { access: memberOnly });

    const sessions = within(await screen.findByRole("list", { name: "سانس‌های ماه" }));
    const [early, late, absent] = sessions.getAllByRole("listitem");
    expect(early).toHaveTextContent("به‌موقع ۱۲:۵۰");
    expect(early).toHaveTextContent("با سارا احمدی");
    expect(late).toHaveTextContent("با تأخیر ۱۳:۲۵");
    expect(late).toHaveTextContent("مرخصی داشت · ۲۵ دقیقه تأخیر");
    expect(late).not.toHaveTextContent("با سارا");
    expect(absent).toHaveTextContent("غایب");

    const totals = within(screen.getByRole("list", { name: "جمع ماه" }));
    expect(totals.getAllByRole("listitem").map((box) => box.textContent)).toEqual([
      "۱به‌موقع",
      "۱با تأخیر",
      "۰بعد از سانس",
      "۱غایب",
      "۰مرخصی",
      "۰لغو شد",
    ]);
    expect(screen.queryByRole("list", { name: "خارج از سانس" })).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "حضور من", level: 1 })).toBeInTheDocument();

    const thisMonth = monthRange(monthOf(gymToday()));
    const asked = new URL(api.requestsTo("GET", path)[0]!.url).searchParams;
    expect([asked.get("from"), asked.get("to")]).toEqual([thisMonth.from, thisMonth.to]);
    expect(screen.getByRole("button", { name: "ماه بعد" })).toBeDisabled();
  });

  it("MyAttendance_EmptyMonth_SaysSo", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, memberOnly.me),
      [`GET ${path}`]: () =>
        json(200, {
          ...month,
          sessions: [],
          totals: { ...month.totals, onTime: 0, late: 0, absent: 0 },
        }),
    });
    renderSite("/my-attendance", { access: memberOnly });

    expect(await screen.findByText("در این ماه سانسی نداشته‌اید.")).toBeInTheDocument();
  });

  it("MyAttendance_PreviousMonth_AsksForItsRange", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, memberOnly.me),
      [`GET ${path}`]: () => json(200, month),
    });
    renderSite("/my-attendance", { access: memberOnly });
    await screen.findByRole("list", { name: "سانس‌های ماه" });

    fireEvent.click(screen.getByRole("button", { name: "ماه قبل" }));

    const before = monthRange(shiftMonth(monthOf(gymToday()), -1));
    await waitFor(() => expect(api.requestsTo("GET", path)).toHaveLength(2));
    const asked = new URL(api.requestsTo("GET", path)[1]!.url).searchParams;
    expect([asked.get("from"), asked.get("to")]).toEqual([before.from, before.to]);
  });

  it("MyAttendance_ATrainer_IsSentHome", async () => {
    const trainerOnly = siteAccess({
      member: null,
      trainer: {
        trainerId: "0199a000-0000-7000-8000-000000000201",
        fullName: "رضا کریمی",
        phoneNumber: "+989351234567",
      },
    });
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, trainerOnly.me) });
    renderSite("/my-attendance", { access: trainerOnly });

    expect(await screen.findByRole("heading", { name: "شاگردان من" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "حضور من" })).not.toBeInTheDocument();
  });
});
