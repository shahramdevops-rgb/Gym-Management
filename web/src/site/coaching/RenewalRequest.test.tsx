import { fireEvent, screen, within } from "@testing-library/react";

import { formatDate, gymToday } from "@/lib/format";
import { json, mockApi, problem } from "@/test/mockApi";

import { renderSite, siteAccess, trainer } from "../test/renderSite";
import { addDays } from "../timetable/labels";

/*
 * «درخواست تمدید» on the site (BUSINESS_RULES.md §15 *The coaching period*, roadmap 15.7o): the
 * member's button, waiting line and decline in «مربی من», and the trainer's «تمدید» / «رد کردن» in
 * «درخواست‌ها».
 */

const trainerOnly = { member: null, trainer };
const renewalId = "0199a000-0000-7000-8000-000000000401";
const acceptPath = `/api/site/coaching/renewal-requests/${renewalId}/accept`;
const declinePath = `/api/site/coaching/renewal-requests/${renewalId}/decline`;
const askPath = "/api/site/coaching/my-trainer/renewal-request";

function myTrainer(renewal: string | null, canAskRenewal: boolean) {
  return {
    trainer: {
      trainerId: "0199a000-0000-7000-8000-000000000201",
      fullName: "رضا کریمی",
      since: "2026-10-20",
      periodEndsOn: addDays(gymToday(), 4),
      renewal,
      canAskRenewal,
    },
    request: null,
  };
}

function renewal(periodEndsOn: string) {
  return {
    requestId: renewalId,
    memberId: "0199a000-0000-7000-8000-000000000101",
    fullName: "سارا احمدی",
    phoneNumber: "+989121234567",
    requestedOn: gymToday(),
    periodEndsOn,
  };
}

function students(renewals: object[], requests: object[] = []) {
  return { requests, renewalRequests: renewals, students: [], periodSoonDays: 7 };
}

function card(heading: string | RegExp) {
  return screen.findByRole("heading", { name: heading }).then((found) => {
    const element = found.closest<HTMLElement>("[data-slot='card']");
    if (element === null) {
      throw new Error(`«${String(heading)}» is not inside a card`);
    }
    return within(element);
  });
}

describe("the member's «درخواست تمدید»", () => {
  it("MyTrainer_LastDays_AsksWithoutAQuestionAndShowsItWaiting", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, myTrainer(null, true)),
      [`POST ${askPath}`]: () => json(200, myTrainer("Pending", false)),
    });
    renderSite("/", { access: siteAccess() });

    const mine = await card("مربی من");
    fireEvent.click(await mine.findByRole("button", { name: "درخواست تمدید" }));

    expect(await mine.findByText("درخواست تمدید در انتظار پاسخ مربی")).toBeInTheDocument();
    expect(mine.queryByRole("button", { name: "درخواست تمدید" })).not.toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(api.requestsTo("POST", askPath)).toHaveLength(1);
  });

  it("MyTrainer_Waiting_CancelTakesItBack", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, myTrainer("Pending", false)),
      [`POST ${askPath}/cancel`]: () => json(200, myTrainer(null, true)),
    });
    renderSite("/", { access: siteAccess() });

    const mine = await card("مربی من");
    fireEvent.click(await mine.findByRole("button", { name: "لغو درخواست" }));

    expect(await mine.findByRole("button", { name: "درخواست تمدید" })).toBeInTheDocument();
    expect(mine.queryByText("درخواست تمدید در انتظار پاسخ مربی")).not.toBeInTheDocument();
    expect(api.requestsTo("POST", `${askPath}/cancel`)).toHaveLength(1);
  });

  it("MyTrainer_Declined_SaysSoAndMayAskAgain", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, myTrainer("Declined", true)),
    });
    renderSite("/", { access: siteAccess() });

    const mine = await card("مربی من");
    expect(await mine.findByText("درخواست تمدید رد شد")).toBeInTheDocument();
    expect(mine.getByRole("button", { name: "درخواست تمدید" })).toBeInTheDocument();
  });

  it("MyTrainer_NotItsLastDays_NoButton", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, myTrainer(null, false)),
    });
    renderSite("/", { access: siteAccess() });

    const mine = await card("مربی من");
    expect(await mine.findByText(/^دورهٔ مربی‌گری تا /)).toBeInTheDocument();
    expect(mine.queryByRole("button", { name: "درخواست تمدید" })).not.toBeInTheDocument();
    expect(mine.queryByText(/درخواست تمدید/)).not.toBeInTheDocument();
  });

  it("MyTrainer_AskRefused_ShowsTheError", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, myTrainer(null, true)),
      [`POST ${askPath}`]: () => problem(422, "Coaching.RenewalNotYet"),
    });
    renderSite("/", { access: siteAccess() });

    const mine = await card("مربی من");
    fireEvent.click(await mine.findByRole("button", { name: "درخواست تمدید" }));

    expect(
      await mine.findByText(
        "درخواست تمدید فقط در روزهای آخر دورهٔ مربی‌گری یا بعد از پایان آن ممکن است.",
      ),
    ).toBeInTheDocument();
  });
});

describe("the trainer's «درخواست‌ها»", () => {
  it("Requests_ARenewalAndARequest_BothCountedAndTheRenewalShowsThePeriod", async () => {
    const lastDay = addDays(gymToday(), 3);
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(
          200,
          students(
            [renewal(lastDay)],
            [
              {
                requestId: "0199a000-0000-7000-8000-000000000301",
                memberId: "0199a000-0000-7000-8000-000000000102",
                fullName: "علی محمدی",
                phoneNumber: "+989351234567",
                requestedOn: gymToday(),
              },
            ],
          ),
        ),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۲)");
    expect(requests.getByText(`درخواست تمدید، دوره تا ${formatDate(lastDay)}`)).toBeInTheDocument();
    expect(requests.getByRole("button", { name: "پذیرفتن" })).toBeInTheDocument();
    expect(requests.getByRole("button", { name: "تمدید" })).toBeInTheDocument();
  });

  it("Requests_OnlyARenewal_TheCardShows", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, students([renewal(addDays(gymToday(), 3))])),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۱)");
    expect(requests.getByText("سارا احمدی")).toBeInTheDocument();
  });

  it("Requests_Renew_FromTheDayAfterTheLastDayAsksFirstThenSendsTheDays", async () => {
    const lastDay = addDays(gymToday(), 3);
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, students([renewal(lastDay)])),
      [`POST ${acceptPath}`]: () => json(200, students([])),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۱)");
    fireEvent.click(requests.getByRole("button", { name: "تمدید" }));
    expect(requests.getByRole("textbox", { name: "تمدید" })).toHaveValue("۳۰");
    const newLastDay = addDays(lastDay, 30);
    expect(
      requests.getByText(`از ${formatDate(addDays(lastDay, 1))} تا ${formatDate(newLastDay)}`),
    ).toBeInTheDocument();
    fireEvent.click(requests.getByRole("button", { name: "تمدید" }));

    const question = await screen.findByRole("dialog");
    expect(question).toHaveTextContent(
      `دورهٔ مربی‌گری سارا احمدی تا ${formatDate(newLastDay)} تمدید شود؟`,
    );
    expect(api.requestsTo("POST", acceptPath)).toHaveLength(0);
    fireEvent.click(within(question).getByRole("button", { name: "بله، تمدید" }));

    await vi.waitFor(() => expect(api.requestsTo("POST", acceptPath)).toHaveLength(1));
    const [sent] = api.requestsTo("POST", acceptPath);
    expect(await sent!.json()).toEqual({ days: 30 });
    await vi.waitFor(() =>
      expect(screen.queryByRole("heading", { name: /^درخواست‌ها/ })).not.toBeInTheDocument(),
    );
  });

  it("Requests_RenewThenCancelTheQuestion_SendsNothing", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, students([renewal(addDays(gymToday(), 3))])),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۱)");
    fireEvent.click(requests.getByRole("button", { name: "تمدید" }));
    fireEvent.click(requests.getByRole("button", { name: "تمدید" }));
    const question = await screen.findByRole("dialog");
    fireEvent.click(within(question).getByRole("button", { name: "انصراف" }));

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("POST", acceptPath)).toHaveLength(0);
  });

  it("Requests_PeriodOver_SaysSoAndRenewsFromToday", async () => {
    const lastDay = addDays(gymToday(), -5);
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, students([renewal(lastDay)])),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۱)");
    expect(
      requests.getByText(`درخواست تمدید، دوره ${formatDate(lastDay)} تمام شد`),
    ).toBeInTheDocument();
    fireEvent.click(requests.getByRole("button", { name: "تمدید" }));
    expect(
      requests.getByText(`از ${formatDate(gymToday())} تا ${formatDate(addDays(gymToday(), 29))}`),
    ).toBeInTheDocument();
  });

  it("Requests_DeclineRenewal_SendsAtOnce", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, students([renewal(addDays(gymToday(), 3))])),
      [`POST ${declinePath}`]: () => json(200, students([])),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۱)");
    fireEvent.click(requests.getByRole("button", { name: "رد کردن" }));

    await vi.waitFor(() => expect(api.requestsTo("POST", declinePath)).toHaveLength(1));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("Requests_RenewRefused_ShowsTheError", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, students([renewal(addDays(gymToday(), 3))])),
      [`POST ${acceptPath}`]: () => problem(409, "Coaching.RenewalNoLongerPending"),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const requests = await card("درخواست‌ها (۱)");
    fireEvent.click(requests.getByRole("button", { name: "تمدید" }));
    fireEvent.click(requests.getByRole("button", { name: "تمدید" }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "بله، تمدید" }),
    );

    expect(
      await screen.findByText("این درخواست تمدید دیگر در انتظار پاسخ نیست. صفحه تازه شد."),
    ).toBeInTheDocument();
  });
});
