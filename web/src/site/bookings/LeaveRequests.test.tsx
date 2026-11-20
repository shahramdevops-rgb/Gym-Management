import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess, trainer } from "../test/renderSite";

/*
 * Leave by request on the trainer's side (BUSINESS_RULES.md §15 *Leave by request*, roadmap 15.7i):
 * «درخواست‌های مرخصی» on the home, and «مرخصی داشت» in the day's attendance.
 */

const trainerOnly = siteAccess({ member: null, trainer });
const listPath = "/api/site/coaching/leave-requests";

const saraRequest = {
  requestId: "0199a000-0000-7000-8000-000000000401",
  memberId: "0199a000-0000-7000-8000-000000000101",
  fullName: "سارا احمدی",
  phoneNumber: "+989121112233",
  // A Monday: 1405/08/11.
  date: "2026-11-02",
  start: "13:00:00",
  end: "14:00:00",
  requestedOn: "2026-10-31",
};

function card() {
  return screen.findByRole("list", { name: "درخواست‌های مرخصی" }).then((list) => within(list));
}

describe("the trainer's leave requests", () => {
  it("Home_NoRequests_NoCard", async () => {
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, trainerOnly.me) });
    renderSite("/", { access: trainerOnly });

    await screen.findByRole("heading", { name: "شاگردان من" });
    expect(screen.queryByRole("heading", { name: /^درخواست‌های مرخصی/ })).not.toBeInTheDocument();
  });

  it("Home_ARequest_ShowsTheStudentTheDateAndTheCount", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${listPath}`]: () => json(200, { requests: [saraRequest] }),
    });
    renderSite("/", { access: trainerOnly });

    const requests = await card();
    expect(screen.getByRole("heading", { name: "درخواست‌های مرخصی (۱)" })).toBeInTheDocument();
    expect(requests.getByText("سارا احمدی")).toBeInTheDocument();
    expect(requests.getByText("دوشنبه ۱۱ آبان، ۱۳:۰۰ تا ۱۴:۰۰")).toBeInTheDocument();
  });

  it.each([
    ["قبول", "accept"],
    ["رد", "decline"],
  ])("Answer_%s_SendsItWithoutAskingAndTheCardGoes", async (button, path) => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${listPath}`]: () => json(200, { requests: [saraRequest] }),
      [`POST ${listPath}/${saraRequest.requestId}/${path}`]: () => json(200, { requests: [] }),
    });
    renderSite("/", { access: trainerOnly });

    fireEvent.click((await card()).getByRole("button", { name: button }));

    await vi.waitFor(() =>
      expect(screen.queryByRole("heading", { name: /^درخواست‌های مرخصی/ })).not.toBeInTheDocument(),
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(api.requestsTo("POST", `${listPath}/${saraRequest.requestId}/${path}`)).toHaveLength(1);
  });

  it("Answer_NoLongerWaiting_SaysSoAndRefreshes", async () => {
    let current = { requests: [saraRequest] };
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      [`GET ${listPath}`]: () => json(200, current),
      [`POST ${listPath}/${saraRequest.requestId}/accept`]: () => {
        current = { requests: [] };
        return problem(409, "Bookings.LeaveNotWaiting");
      },
    });
    renderSite("/", { access: trainerOnly });

    fireEvent.click((await card()).getByRole("button", { name: "قبول" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("دیگر در انتظار پاسخ نیست");
    await vi.waitFor(() =>
      expect(screen.queryByRole("button", { name: "قبول" })).not.toBeInTheDocument(),
    );
  });

  it("DayPage_CameAnywayWithLeave_SaysHadLeave", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly.me),
      "GET /api/site/coaching/today": () =>
        json(200, {
          date: "2026-11-07",
          isToday: true,
          outside: [],
          totals: { students: 1, present: 1, late: 1, outsideSession: 0, absent: 0 },
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
                  memberId: saraRequest.memberId,
                  fullName: saraRequest.fullName,
                  phoneNumber: saraRequest.phoneNumber,
                  state: "Late",
                  checkedInAt: "13:25:00",
                  checkedOutAt: null,
                  autoCheckedOut: false,
                  minutesEarly: null,
                  minutesLate: 25,
                  hadLeave: true,
                },
              ],
            },
          ],
        }),
    });
    renderSite("/today", { access: trainerOnly });

    const row = within(
      await screen.findByRole("list", { name: "شاگردان ۱۳:۰۰ تا ۱۴:۰۰" }),
    ).getByRole("listitem");
    expect(row).toHaveTextContent("با تأخیر ۱۳:۲۵");
    expect(row).toHaveTextContent("مرخصی داشت · ۲۵ دقیقه تأخیر");
  });
});
