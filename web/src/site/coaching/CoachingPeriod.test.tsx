import { fireEvent, screen, within } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import { json, mockApi, problem } from "@/test/mockApi";

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
const daysLeftPath = `${periodPath}/days-left`;

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

  it.each([
    [["مینا رضایی"], "دورهٔ مینا رضایی رسیدگی می‌خواهد."],
    [["مینا رضایی", "سارا احمدی"], "دورهٔ مینا رضایی و سارا احمدی رسیدگی می‌خواهد."],
  ])("Home_FewPeriodsNeedTheTrainer_NamesThem %#", async (names, sentence) => {
    const now = gymToday();
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, {
          requests: [],
          periodSoonDays: 7,
          students: [
            ...names.map((fullName) => studentRow({ fullName, periodEndsOn: addDays(now, 1) })),
            studentRow({ fullName: "ت دور", periodEndsOn: addDays(now, 20) }),
          ],
        }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    expect(await screen.findByText(sentence)).toBeInTheDocument();
  });

  it("Students_PeriodNeedsTheTrainer_Marked", async () => {
    const now = gymToday();
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, {
          requests: [],
          periodSoonDays: 7,
          students: [
            studentRow({ fullName: "الف قدیمی", periodEndsOn: null }),
            studentRow({ fullName: "ب دو روز", periodEndsOn: addDays(now, 1) }),
            studentRow({ fullName: "پ تمام", periodEndsOn: addDays(now, -1) }),
            studentRow({ fullName: "ت دور", periodEndsOn: addDays(now, 20) }),
          ],
        }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    // The home's card counts the active ones and names those that need the trainer (15.7l: the
    // one whose period is over is inactive, so neither counted nor named).
    expect(
      await screen.findByText("دورهٔ الف قدیمی و ب دو روز رسیدگی می‌خواهد."),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole("link", { name: "شاگردان من (۳)" }));

    const rows = within(await screen.findByRole("list", { name: "شاگردان من" })).getAllByRole(
      "listitem",
    );
    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent("دوره را مشخص کنید");
    expect(rows[1]).toHaveTextContent("۲ روز");
    expect(rows[1]).not.toHaveTextContent("مانده");
    expect(rows[2]).not.toHaveTextContent(/روز|دوره/);

    fireEvent.click(screen.getByRole("button", { name: "غیرفعال (۱)" }));
    expect(
      within(screen.getByRole("list", { name: "شاگردان غیرفعال" })).getByRole("listitem"),
    ).toHaveTextContent("دوره تمام شد");
  });

  it.each([
    [7, ["۷ روز", null]],
    [10, ["۷ روز", "۸ روز"]],
  ])(
    "Students_MarkFrom%sDaysLeft_FollowsTheSmsSettingsNumber",
    async (periodSoonDays, expected) => {
      // 15.7n: the days the student's SMS goes from, sent by the API, in place of the 3 days of 15.7h.
      const now = gymToday();
      mockApi({
        "GET /api/site/me": () => json(200, trainerOnly),
        "GET /api/site/coaching/students": () =>
          json(200, {
            requests: [],
            periodSoonDays,
            students: [
              studentRow({ fullName: "الف هفت", periodEndsOn: addDays(now, 6) }),
              studentRow({ fullName: "ب هشت", periodEndsOn: addDays(now, 7) }),
            ],
          }),
      });
      renderSite("/students", { access: siteAccess(trainerOnly) });

      const rows = within(await screen.findByRole("list", { name: "شاگردان من" })).getAllByRole(
        "listitem",
      );
      expected.forEach((mark, index) => {
        if (mark === null) {
          expect(rows[index]).not.toHaveTextContent(/روز/);
        } else {
          expect(rows[index]).toHaveTextContent(mark);
        }
      });
    },
  );

  it("Students_PeriodOver_InTheClosedInactivePartThatOpensOnATap", async () => {
    const now = gymToday();
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, {
          requests: [],
          periodSoonDays: 7,
          students: [
            studentRow({ fullName: "سارا فعال", periodEndsOn: addDays(now, 20) }),
            // Its last day is today: still running, so still active.
            studentRow({ fullName: "لیلا روز آخر", periodEndsOn: now }),
            studentRow({ fullName: "مینا تمام", periodEndsOn: addDays(now, -3) }),
            studentRow({ fullName: "رضا تمام", periodEndsOn: addDays(now, -40) }),
          ],
        }),
    });
    renderSite("/students", { access: siteAccess(trainerOnly) });

    // The title counts the active ones, as the home's card does.
    expect(
      await screen.findByRole("heading", { name: "شاگردان من (۲)", level: 1 }),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole("list", { name: "شاگردان من" })).getAllByRole("listitem"),
    ).toHaveLength(2);

    const part = screen.getByRole("button", { name: "غیرفعال (۲)" });
    expect(part).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("list", { name: "شاگردان غیرفعال" })).not.toBeInTheDocument();

    fireEvent.click(part);

    expect(part).toHaveAttribute("aria-expanded", "true");
    const inactive = within(screen.getByRole("list", { name: "شاگردان غیرفعال" }));
    expect(inactive.getAllByRole("listitem")).toHaveLength(2);
    expect(inactive.getByText("مینا تمام")).toBeInTheDocument();
    // Still the trainer's students: their attendance and program open as the active ones' do.
    expect(inactive.getByRole("link", { name: "برنامهٔ ثابت رضا تمام" })).toBeInTheDocument();

    fireEvent.click(part);
    expect(screen.queryByRole("list", { name: "شاگردان غیرفعال" })).not.toBeInTheDocument();
  });

  it("Students_SearchFindsAnInactiveStudent_ShowsThemWithoutATap", async () => {
    const now = gymToday();
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, {
          requests: [],
          periodSoonDays: 7,
          students: [
            studentRow({ fullName: "سارا فعال", periodEndsOn: addDays(now, 20) }),
            studentRow({ fullName: "مینا تمام", periodEndsOn: addDays(now, -3) }),
            studentRow({ fullName: "رضا تمام", periodEndsOn: addDays(now, -40) }),
          ],
        }),
    });
    renderSite("/students", { access: siteAccess(trainerOnly) });
    await screen.findByRole("list", { name: "شاگردان من" });

    fireEvent.change(screen.getByRole("searchbox", { name: "جست‌وجوی شاگرد" }), {
      target: { value: "مینا" },
    });

    expect(screen.queryByRole("list", { name: "شاگردان من" })).not.toBeInTheDocument();
    expect(screen.queryByText("شاگرد فعالی ندارید.")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "غیرفعال (۱)" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
    const inactive = within(screen.getByRole("list", { name: "شاگردان غیرفعال" }));
    expect(inactive.getAllByRole("listitem")).toHaveLength(1);
    expect(inactive.getByText("مینا تمام")).toBeInTheDocument();
  });

  it("Students_EveryPeriodOver_SaysNoActiveStudentAboveTheClosedPart", async () => {
    const now = gymToday();
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, {
          requests: [],
          periodSoonDays: 7,
          students: [studentRow({ fullName: "مینا تمام", periodEndsOn: addDays(now, -3) })],
        }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    expect(await screen.findByRole("link", { name: "شاگردان من (۰)" })).toBeInTheDocument();
    expect(screen.queryByText(/رسیدگی می‌خواهد/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("link", { name: "شاگردان من (۰)" }));

    expect(await screen.findByText("شاگرد فعالی ندارید.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "غیرفعال (۱)" })).toBeInTheDocument();
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

    // Asked first (15.7k): nothing is sent before «بله، تمدید».
    const question = await screen.findByRole("dialog");
    expect(question).toHaveTextContent("دورهٔ مربی‌گری سارا احمدی تا ۱۴۰۵/۰۹/۲۹ تمدید شود؟");
    expect(api.requestsTo("POST", periodPath)).toHaveLength(0);
    fireEvent.click(within(question).getByRole("button", { name: "بله، تمدید" }));

    expect(await period.findByText(/^دورهٔ مربی‌گری تا ۱۴۰۵\/۰۹\/۲۹/)).toBeInTheDocument();
    const [sent] = api.requestsTo("POST", periodPath);
    expect(await sent!.json()).toEqual({ days: 30 });
  });

  it("Schedule_RenewThenCancelTheQuestion_SendsNothing", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student("2026-11-20")),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const period = await card("دورهٔ مربی‌گری");
    fireEvent.click(period.getByRole("button", { name: "تمدید" }));
    fireEvent.click(period.getByRole("button", { name: "تمدید" }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "انصراف" }),
    );

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("POST", periodPath)).toHaveLength(0);
    // The box stays, with the days typed, to change or cancel.
    expect(period.getByRole("textbox", { name: "تمدید" })).toHaveValue("۳۰");
  });

  it("Schedule_EditFewerDays_WarnsOfTheCutAsksAndSendsTheDaysLeft", async () => {
    const schedule = {
      startsOn: "2026-11-01",
      endsOn: "2026-12-25",
      slots: [{ day: "Saturday", start: "13:00:00", end: "14:00:00" }],
    };
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      // Renewed by mistake: the period runs to 1405/11/26.
      [`GET ${schedulePath}`]: () => json(200, student("2027-02-14", schedule)),
      [`POST ${daysLeftPath}`]: () =>
        json(200, student("2026-12-09", { ...schedule, endsOn: "2026-12-09" })),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const period = await card("دورهٔ مربی‌گری");
    fireEvent.click(period.getByRole("button", { name: "ویرایش دوره" }));
    const box = period.getByRole("textbox", { name: "روزهای باقی‌مانده" });
    // Starts at the days left now, today counted.
    expect(box).toHaveValue("۹۷");
    fireEvent.change(box, { target: { value: "30" } });
    expect(period.getByText("از ۱۴۰۵/۰۸/۱۹ تا ۱۴۰۵/۰۹/۱۸")).toBeInTheDocument();
    expect(period.getByText(/با این دوره .* تمام می‌شود/)).toHaveTextContent("۱۴۰۵/۰۹/۱۸");
    fireEvent.click(period.getByRole("button", { name: "ثبت" }));

    const question = await screen.findByRole("dialog");
    expect(question).toHaveTextContent("روز آخر دورهٔ سارا احمدی ۱۴۰۵/۰۹/۱۸ شود؟");
    fireEvent.click(within(question).getByRole("button", { name: "بله، ثبت" }));

    expect(await period.findByText(/^دورهٔ مربی‌گری تا ۱۴۰۵\/۰۹\/۱۸/)).toBeInTheDocument();
    const [sent] = api.requestsTo("POST", daysLeftPath);
    expect(await sent!.json()).toEqual({ daysLeft: 30 });
    expect(api.requestsTo("POST", periodPath)).toHaveLength(0);
  });

  it("Schedule_EditRefused_ShowsTheError", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student("2026-11-20")),
      [`POST ${daysLeftPath}`]: () => problem(409, "Coaching.PeriodNotRunning"),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const period = await card("دورهٔ مربی‌گری");
    fireEvent.click(period.getByRole("button", { name: "ویرایش دوره" }));
    fireEvent.click(period.getByRole("button", { name: "ثبت" }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "بله، ثبت" }),
    );

    expect(await period.findByText(/در جریان نیست و ویرایش نمی‌شود/)).toBeInTheDocument();
  });

  it("Schedule_PeriodOver_OnlyRenewNoEdit", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student("2026-11-05")),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const period = await card("دورهٔ مربی‌گری");
    expect(period.getByRole("button", { name: "تمدید" })).toBeInTheDocument();
    expect(period.queryByRole("button", { name: "ویرایش دوره" })).not.toBeInTheDocument();
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
