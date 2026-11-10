import { fireEvent, screen, within } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import { json, mockApi } from "@/test/mockApi";

import { emptyTimetable, renderSite, siteAccess, trainer } from "../test/renderSite";
import { addDays } from "../timetable/labels";

/*
 * The coaching period on the site (BUSINESS_RULES.md §15 *The coaching period*, roadmap 15.7h): the
 * days on «پذیرفتن», the marks in «شاگردان من», the member's line, and «تمدید» on «برنامهٔ ثابت».
 */

const trainerOnly = { member: null, trainer };
const memberId = "0199a000-0000-7000-8000-000000000101";
const requestId = "0199a000-0000-7000-8000-000000000301";
const acceptPath = `/api/site/coaching/requests/${requestId}/accept`;
const schedulePath = `/api/site/coaching/students/${memberId}/schedule`;
const periodPath = `/api/site/coaching/students/${memberId}/period`;

// The schedule page reads its today from the API: a Saturday, 1405/08/19.
const today = "2026-11-10";

const request = {
  requestId,
  memberId,
  fullName: "علی محمدی",
  phoneNumber: "+989121234567",
  requestedOn: "2026-11-09",
};

function studentRow(overrides: object) {
  return {
    memberId: crypto.randomUUID(),
    fullName: "علی محمدی",
    phoneNumber: "+989121234567",
    since: "2026-10-01",
    isActive: true,
    periodEndsOn: null,
    ...overrides,
  };
}

function student(periodEndsOn: string | null, schedule: unknown = null) {
  return {
    today,
    lastDay: "2027-02-01",
    memberId,
    fullName: "سارا احمدی",
    periodEndsOn,
    schedule,
    sessions: [],
  };
}

function card(heading: string) {
  return screen.findByRole("heading", { name: heading }).then((found) => {
    const element = found.closest<HTMLElement>("[data-slot='card']");
    if (element === null) {
      throw new Error(`«${heading}» is not inside a card`);
    }
    return within(element);
  });
}

describe("the coaching period", () => {
  it("Accept_DaysTypedInPersian_SendsThemAndShowsTheDates", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [request], students: [] }),
      [`POST ${acceptPath}`]: () => json(200, { requests: [], students: [] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۱)");
    fireEvent.click(requests.getByRole("button", { name: "پذیرفتن" }));
    fireEvent.change(requests.getByRole("textbox", { name: "دورهٔ مربی‌گری" }), {
      target: { value: "۴۵" },
    });
    expect(requests.getByText(/^از .* تا /)).toBeInTheDocument();
    fireEvent.click(requests.getByRole("button", { name: "پذیرفتن" }));

    await vi.waitFor(() => expect(api.requestsTo("POST", acceptPath)).toHaveLength(1));
    const [sent] = api.requestsTo("POST", acceptPath);
    expect(await sent!.json()).toEqual({ days: 45 });
  });

  it.each(["6", "85", ""])("Accept_Days%sOutOfRange_CannotBeSent", async (typed) => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [request], students: [] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۱)");
    fireEvent.click(requests.getByRole("button", { name: "پذیرفتن" }));
    fireEvent.change(requests.getByRole("textbox", { name: "دورهٔ مربی‌گری" }), {
      target: { value: typed },
    });

    expect(requests.getByText("بین ۷ تا ۸۴ روز")).toBeInTheDocument();
    expect(requests.getByRole("button", { name: "پذیرفتن" })).toBeDisabled();
    expect(api.requestsTo("POST", acceptPath)).toHaveLength(0);
  });

  it("Students_PeriodNeedsTheTrainer_Marked", async () => {
    const now = gymToday();
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, {
          requests: [],
          students: [
            studentRow({ fullName: "الف قدیمی", periodEndsOn: null }),
            studentRow({ fullName: "ب دو روز", periodEndsOn: addDays(now, 1) }),
            studentRow({ fullName: "پ تمام", periodEndsOn: addDays(now, -1) }),
            studentRow({ fullName: "ت دور", periodEndsOn: addDays(now, 20) }),
          ],
        }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const rows = within(await screen.findByRole("list", { name: "شاگردان من" })).getAllByRole(
      "listitem",
    );
    expect(rows[0]).toHaveTextContent("دوره را مشخص کنید");
    expect(rows[1]).toHaveTextContent("۲ روز مانده");
    expect(rows[2]).toHaveTextContent("دوره تمام شد");
    expect(rows[3]).not.toHaveTextContent(/روز مانده|دوره/);
  });

  it("MyTrainer_WithAPeriod_ShowsItsLastDayAndTheDaysLeft", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () =>
        json(200, {
          trainer: {
            trainerId: "0199a000-0000-7000-8000-000000000201",
            fullName: "رضا کریمی",
            since: "2026-10-20",
            periodEndsOn: addDays(gymToday(), 11),
          },
          request: null,
        }),
    });
    renderSite("/", { access: siteAccess() });

    const myTrainer = await card("مربی من");
    expect(await myTrainer.findByText(/^دورهٔ مربی‌گری تا /)).toHaveTextContent("۱۲ روز مانده");
  });

  it("Schedule_Renew_StartsAfterTheLastDayAndSendsTheDays", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student("2026-11-20")),
      [`POST ${periodPath}`]: () => json(200, student("2026-12-20")),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const period = await card("دورهٔ مربی‌گری");
    expect(period.getByText(/^دورهٔ مربی‌گری تا /)).toHaveTextContent("۱۴۰۵/۰۸/۲۹، ۱۱ روز مانده");
    fireEvent.click(period.getByRole("button", { name: "تمدید" }));
    expect(period.getByRole("textbox", { name: "تمدید" })).toHaveValue("۳۰");
    // From the day after the last one, for 30 days.
    expect(period.getByText("از ۱۴۰۵/۰۸/۳۰ تا ۱۴۰۵/۰۹/۲۹")).toBeInTheDocument();
    fireEvent.click(period.getByRole("button", { name: "تمدید" }));

    expect(await period.findByText(/^دورهٔ مربی‌گری تا ۱۴۰۵\/۰۹\/۲۹/)).toBeInTheDocument();
    const [sent] = api.requestsTo("POST", periodPath);
    expect(await sent!.json()).toEqual({ days: 30 });
  });

  it("Schedule_StudentFromBefore_AsksTheDaysLeftAndWarnsOfTheCut", async () => {
    const schedule = {
      startsOn: "2026-11-01",
      endsOn: "2026-12-25",
      slots: [{ day: "Saturday", start: "13:00:00", end: "14:00:00" }],
    };
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student(null, schedule)),
      [`POST ${periodPath}`]: () =>
        json(200, student("2026-11-14", { ...schedule, endsOn: "2026-11-14" })),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const period = await card("دورهٔ مربی‌گری");
    expect(period.getByText(/پیش از دوره‌ها/)).toBeInTheDocument();
    const box = period.getByRole("textbox", { name: "روزهای باقی‌مانده" });
    expect(box).toHaveValue("");
    expect(period.queryByRole("button", { name: "انصراف" })).not.toBeInTheDocument();
    fireEvent.change(box, { target: { value: "5" } });
    expect(period.getByText(/با این دوره .* تمام می‌شود/)).toHaveTextContent("۱۴۰۵/۰۸/۲۳");
    fireEvent.click(period.getByRole("button", { name: "ثبت" }));

    expect(await period.findByText(/^دورهٔ مربی‌گری تا /)).toHaveTextContent("۱۴۰۵/۰۸/۲۳");
    const [sent] = api.requestsTo("POST", periodPath);
    expect(await sent!.json()).toEqual({ days: 5 });
  });

  it("Schedule_StudentFromBeforeOneDayLeft_CanBeSent", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student(null)),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const period = await card("دورهٔ مربی‌گری");
    fireEvent.change(period.getByRole("textbox", { name: "روزهای باقی‌مانده" }), {
      target: { value: "1" },
    });

    expect(period.getByRole("button", { name: "ثبت" })).toBeEnabled();
  });

  it("Schedule_PeriodOver_NoNewProgramUntilRenewed", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student("2026-11-05")),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const period = await card("دورهٔ مربی‌گری");
    expect(period.getByText(/تمام شد$/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "ساختن برنامه" })).toBeDisabled();
    expect(screen.getByText(/اول دوره را تمدید کنید/)).toBeInTheDocument();
  });

  it("Schedule_Build_EndsOnThePeriodsLastDayByDefault", async () => {
    const pattern = {
      startsOn: today,
      sessions: [{ day: "Saturday", start: "13:00:00", end: "14:00:00", capacity: 8 }],
    };
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, { ...emptyTimetable(today), pattern }),
      [`GET ${schedulePath}`]: () => json(200, student("2026-12-09")),
      [`POST ${schedulePath}/check`]: () => json(200, { sessions: 4, full: [] }),
      [`PUT ${schedulePath}`]: () => json(200, student("2026-12-09")),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "ساختن برنامه" }));
    fireEvent.click(await screen.findByRole("button", { name: "شنبه ۱۳:۰۰ تا ۱۴:۰۰" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره برنامه" }));

    await vi.waitFor(() => expect(api.requestsTo("PUT", schedulePath)).toHaveLength(1));
    const [sent] = api.requestsTo("PUT", schedulePath);
    expect(await sent!.json()).toMatchObject({ startsOn: today, endsOn: "2026-12-09" });
  });
});
