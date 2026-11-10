import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import { emptyHome, emptyTimetable, renderSite, siteAccess, trainer } from "../test/renderSite";

const trainerOnly = { member: null, trainer };

// A Saturday: 1405/08/09.
const today = "2026-10-31";
const memberId = "0199a000-0000-7000-8000-000000000301";
const schedulePath = `/api/site/coaching/students/${memberId}/schedule`;

const pattern = {
  startsOn: today,
  sessions: [
    { day: "Saturday", start: "13:00:00", end: "14:00:00", capacity: 8 },
    { day: "Saturday", start: "14:00:00", end: "15:00:00", capacity: 6 },
    { day: "Monday", start: "13:00:00", end: "14:00:00", capacity: 8 },
  ],
};

const timetable = { ...emptyTimetable(today), pattern };

function student(schedule: unknown = null, sessions: unknown[] = []) {
  return {
    today,
    lastDay: "2027-01-22",
    memberId,
    fullName: "سارا احمدی",
    schedule,
    sessions,
  };
}

const saturdays = {
  startsOn: today,
  endsOn: "2026-12-25",
  slots: [{ day: "Saturday", start: "13:00:00", end: "14:00:00" }],
};

async function bodyOf(request: Request) {
  return (await request.json()) as unknown;
}

describe("the trainer's «برنامهٔ ثابت» of a student", () => {
  it("Home_Student_LinksToTheirFixedSchedule", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, {
          requests: [],
          students: [
            {
              memberId,
              fullName: "سارا احمدی",
              phoneNumber: "+989121112233",
              since: "2026-10-20",
              isActive: true,
            },
          ],
        }),
    });

    renderSite("/", { access: siteAccess(trainerOnly) });

    expect(await screen.findByRole("link", { name: "برنامهٔ ثابت سارا احمدی" })).toHaveAttribute(
      "href",
      `/students/${memberId}`,
    );
  });

  it("Build_PicksASessionOfThePattern_SendsEightWeeksFromToday", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetable),
      [`GET ${schedulePath}`]: () => json(200, student()),
      [`POST ${schedulePath}/check`]: () => json(200, { sessions: 8, full: [] }),
      [`PUT ${schedulePath}`]: () => json(200, student(saturdays)),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    expect(await screen.findByText("سارا احمدی")).toBeInTheDocument();
    expect(screen.getByText(/هنوز برنامهٔ ثابتی ندارد/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "ساختن برنامه" }));

    const choices = within(await screen.findByRole("list", { name: "سانس‌های الگو" }));
    expect(
      choices.getAllByRole("button").map((button) => button.getAttribute("aria-label")),
    ).toEqual(["شنبه ۱۳:۰۰ تا ۱۴:۰۰", "شنبه ۱۴:۰۰ تا ۱۵:۰۰", "دوشنبه ۱۳:۰۰ تا ۱۴:۰۰"]);
    const saturday = choices.getByRole("button", { name: "شنبه ۱۳:۰۰ تا ۱۴:۰۰" });
    fireEvent.click(saturday);
    expect(saturday).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(screen.getByRole("button", { name: "ذخیره برنامه" }));

    expect(await screen.findByRole("list", { name: "سانس‌های هفتگی" })).toHaveTextContent(
      "شنبه ۱۳:۰۰ تا ۱۴:۰۰",
    );
    const [checked] = api.requestsTo("POST", `${schedulePath}/check`);
    expect(await bodyOf(checked!)).toEqual(saturdays);
    const [sent] = api.requestsTo("PUT", schedulePath);
    expect(await bodyOf(sent!)).toEqual(saturdays);
  });

  it("Build_ADateHasNoRoom_NamesItAndSavesNothing", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetable),
      [`GET ${schedulePath}`]: () => json(200, student()),
      [`POST ${schedulePath}/check`]: () =>
        json(200, {
          sessions: 8,
          full: [{ date: "2026-11-07", start: "13:00:00", end: "14:00:00" }],
        }),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "ساختن برنامه" }));
    fireEvent.click(await screen.findByRole("button", { name: "شنبه ۱۳:۰۰ تا ۱۴:۰۰" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره برنامه" }));

    expect(await screen.findByRole("list", { name: "سانس‌های پر" })).toHaveTextContent(
      "شنبه ۱۶ آبان، ۱۳:۰۰ تا ۱۴:۰۰",
    );
    expect(api.requestsTo("PUT", schedulePath)).toHaveLength(0);
  });

  it("Change_AStartedSchedule_AppliesFromNow", async () => {
    const started = { ...saturdays, startsOn: "2026-10-24" };
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetable),
      [`GET ${schedulePath}`]: () => json(200, student(started)),
      [`POST ${schedulePath}/check`]: () => json(200, { sessions: 16, full: [] }),
      [`PUT ${schedulePath}`]: () => json(200, student(started)),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر برنامه" }));
    expect(
      await screen.findByRole("button", { name: "شنبه ۱۳:۰۰ تا ۱۴:۰۰", pressed: true }),
    ).toBeInTheDocument();
    expect(screen.getByText(/تغییر از همین حالا اعمال می‌شود/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "دوشنبه ۱۳:۰۰ تا ۱۴:۰۰" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره برنامه" }));

    await screen.findByRole("button", { name: "تغییر برنامه" });
    const [sent] = api.requestsTo("PUT", schedulePath);
    expect(await bodyOf(sent!)).toEqual({
      startsOn: "2026-10-31",
      endsOn: "2026-12-25",
      slots: [
        { day: "Saturday", start: "13:00:00", end: "14:00:00" },
        { day: "Monday", start: "13:00:00", end: "14:00:00" },
      ],
    });
  });

  it("Change_ASessionThePatternNoLongerHas_IsShownOffAndNotSent", async () => {
    const old = {
      ...saturdays,
      startsOn: "2026-10-24",
      slots: [
        { day: "Saturday", start: "13:00:00", end: "14:00:00" },
        { day: "Sunday", start: "10:00:00", end: "12:00:00" },
      ],
    };
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, timetable),
      [`GET ${schedulePath}`]: () => json(200, student(old)),
      [`POST ${schedulePath}/check`]: () => json(200, { sessions: 8, full: [] }),
      [`PUT ${schedulePath}`]: () => json(200, student(saturdays)),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "تغییر برنامه" }));
    const gone = await screen.findByRole("button", {
      name: "یکشنبه ۱۰:۰۰ تا ۱۲:۰۰، دیگر در الگوی شما نیست",
    });
    expect(gone).toBeDisabled();
    expect(gone).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: "شنبه ۱۳:۰۰ تا ۱۴:۰۰" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    fireEvent.click(screen.getByRole("button", { name: "ذخیره برنامه" }));

    await screen.findByRole("button", { name: "تغییر برنامه" });
    const [sent] = api.requestsTo("PUT", schedulePath);
    expect(await bodyOf(sent!)).toMatchObject({
      slots: [{ day: "Saturday", start: "13:00:00", end: "14:00:00" }],
    });
  });

  it("Build_AVipSessionOfThePattern_SaysSoOnItsChoice", async () => {
    const vipMonday = {
      ...pattern,
      sessions: [{ day: "Monday", start: "13:00:00", end: "14:00:00", capacity: 8, isVip: true }],
    };
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/timetable": () => json(200, { ...timetable, pattern: vipMonday }),
      [`GET ${schedulePath}`]: () => json(200, student()),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "ساختن برنامه" }));

    expect(
      await screen.findByRole("button", { name: "دوشنبه ۱۳:۰۰ تا ۱۴:۰۰، VIP" }),
    ).toHaveTextContent("VIP");
  });

  it("Dates_AVipSession_IsLabelled", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () =>
        json(
          200,
          student(saturdays, [
            {
              date: "2026-11-02",
              start: "13:00:00",
              end: "14:00:00",
              isVip: true,
              moved: false,
              removed: false,
              started: false,
              left: false,
            },
          ]),
        ),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const dates = within(await screen.findByRole("list", { name: "سانس‌های پیش رو" }));
    expect(dates.getByRole("listitem")).toHaveTextContent("VIP");
  });

  it("Dates_OneWeekAtATime_ShowEachOneWithWhatChanged", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () =>
        json(
          200,
          student(saturdays, [
            {
              date: today,
              start: "13:00:00",
              end: "14:00:00",
              moved: false,
              removed: false,
              started: true,
              left: false,
            },
            {
              date: "2026-11-02",
              start: "13:00:00",
              end: "14:00:00",
              moved: true,
              removed: false,
              started: false,
              left: false,
            },
            {
              date: "2026-11-07",
              start: "13:00:00",
              end: "14:00:00",
              moved: false,
              removed: true,
              started: false,
              left: false,
            },
          ]),
        ),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    const firstWeek = within(await screen.findByRole("list", { name: "سانس‌های پیش رو" }));
    expect(screen.getByText("هفتهٔ ۱ از ۲")).toBeInTheDocument();
    const items = firstWeek.getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent("شنبه ۹ آبان");
    expect(items[1]).toHaveTextContent("جابه‌جا شده");

    fireEvent.click(screen.getByRole("button", { name: "هفتهٔ بعد" }));

    const secondWeek = within(screen.getByRole("list", { name: "سانس‌های پیش رو" }));
    expect(secondWeek.getAllByRole("listitem")).toHaveLength(1);
    expect(secondWeek.getByRole("listitem")).toHaveTextContent("شنبه ۱۶ آبان");
    expect(secondWeek.getByRole("listitem")).toHaveTextContent("لغو شد");
    expect(screen.getByRole("button", { name: "هفتهٔ بعد" })).toBeDisabled();
  });

  it("End_AsksFirst_ThenEndsTheSchedule", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student(saturdays)),
      [`DELETE ${schedulePath}`]: () => json(200, student()),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "پایان برنامه" }));
    const confirm = await screen.findByRole("dialog", { name: "پایان برنامه" });
    expect(confirm).toHaveTextContent("برنامهٔ ثابت سارا احمدی از همین حالا تمام می‌شود");
    fireEvent.click(within(confirm).getByRole("button", { name: "پایان برنامه" }));

    expect(await screen.findByText(/هنوز برنامهٔ ثابتی ندارد/)).toBeInTheDocument();
    expect(api.requestsTo("DELETE", schedulePath)).toHaveLength(1);
  });

  it("Page_NoLongerAStudent_ShowsTheReasonInPersian", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => problem(422, "Bookings.NotAStudent"),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    // The site's reads try once more before failing.
    expect(
      await screen.findByText("این عضو الان شاگرد شما نیست.", {}, { timeout: 3000 }),
    ).toBeInTheDocument();
  });

  it("EndCoaching_AsksFirst_EndsAndGoesHome", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student()),
      [`POST /api/site/coaching/students/${memberId}/end`]: () =>
        json(200, { requests: [], students: [] }),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "پایان مربی‌گری" }));
    const question = await screen.findByRole("dialog");
    expect(question).toHaveTextContent(
      "مربی‌گری شما برای سارا احمدی از امروز تمام می‌شود. ادامه می‌دهید؟",
    );
    fireEvent.click(within(question).getByRole("button", { name: "پایان مربی‌گری" }));

    expect(await screen.findByText("هنوز شاگردی ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/site/coaching/students/${memberId}/end`)).toHaveLength(1);
  });

  it("EndCoaching_Cancel_SendsNothing", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      [`GET ${schedulePath}`]: () => json(200, student()),
    });
    renderSite(`/students/${memberId}`, { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("button", { name: "پایان مربی‌گری" }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "انصراف" }),
    );

    expect(screen.getByRole("heading", { name: "برنامهٔ ثابت", level: 1 })).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/site/coaching/students/${memberId}/end`)).toHaveLength(0);
  });
});
