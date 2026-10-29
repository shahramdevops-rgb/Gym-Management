import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import { renderSite, siteAccess } from "../test/renderSite";

const reza = { trainerId: "0199a000-0000-7000-8000-000000000201", fullName: "رضا کریمی" };
const sara = { trainerId: "0199a000-0000-7000-8000-000000000202", fullName: "سارا احمدی" };

const withTrainer = { trainer: { ...reza, since: "2026-10-20" }, request: null };
const changing = {
  trainer: { ...reza, since: "2026-10-20" },
  request: {
    requestId: "0199a000-0000-7000-8000-000000000301",
    ...sara,
    requestedOn: "2026-10-29",
  },
};

function myTrainerCard() {
  return screen.findByRole("heading", { name: "مربی من" }).then((heading) => {
    const card = heading.closest<HTMLElement>("[data-slot='card']");
    if (card === null) {
      throw new Error("«مربی من» is not inside a card");
    }
    return within(card);
  });
}

describe("MyTrainerCard", () => {
  it("MyTrainer_NoTrainer_OffersToPickOne", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, { trainer: null, request: null }),
    });

    renderSite("/", { access: siteAccess() });

    const card = await myTrainerCard();
    expect(await card.findByText("هنوز مربی ندارید.")).toBeInTheDocument();
    expect(card.getByRole("link", { name: "انتخاب مربی" })).toHaveAttribute("href", "/trainers");
  });

  it("MyTrainer_ChangingTrainer_ShowsTheTrainerAndTheWaitingRequest", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, changing),
    });

    renderSite("/", { access: siteAccess() });

    const card = await myTrainerCard();
    expect(await card.findByText("رضا کریمی")).toBeInTheDocument();
    expect(card.getByText(/در انتظار پاسخ است/)).toHaveTextContent("سارا احمدی");
    expect(card.getByRole("link", { name: "تغییر مربی" })).toBeInTheDocument();
  });

  it("MyTrainer_Cancel_TakesTheRequestBack", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, changing),
      "POST /api/site/coaching/my-trainer/cancel-request": () => json(200, withTrainer),
    });
    renderSite("/", { access: siteAccess() });
    const card = await myTrainerCard();

    fireEvent.click(await card.findByRole("button", { name: "لغو" }));

    await card.findByRole("link", { name: "تغییر مربی" });
    expect(await screen.findByText("رضا کریمی")).toBeInTheDocument();
    expect(card.queryByText(/در انتظار پاسخ است/)).not.toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/coaching/my-trainer/cancel-request")).toHaveLength(1);
  });

  it("MyTrainer_End_AsksFirstAndSendsNothingOnCancel", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, withTrainer),
      "POST /api/site/coaching/my-trainer/end": () => json(200, { trainer: null, request: null }),
    });
    renderSite("/", { access: siteAccess() });
    const card = await myTrainerCard();

    fireEvent.click(await card.findByRole("button", { name: "پایان مربی‌گری" }));
    const question = await screen.findByRole("dialog");
    expect(question).toHaveTextContent(
      "مربی‌گری رضا کریمی برای شما از امروز تمام می‌شود. ادامه می‌دهید؟",
    );
    fireEvent.click(within(question).getByRole("button", { name: "انصراف" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/coaching/my-trainer/end")).toHaveLength(0);

    fireEvent.click(card.getByRole("button", { name: "پایان مربی‌گری" }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "پایان مربی‌گری" }),
    );

    expect(await card.findByText("هنوز مربی ندارید.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/coaching/my-trainer/end")).toHaveLength(1);
  });

  it("MyTrainer_TrainerEndedItMeanwhile_SaysSoAndShowsWhatIsTrueNow", async () => {
    let current: unknown = withTrainer;
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer": () => json(200, current),
      "POST /api/site/coaching/my-trainer/end": () => {
        current = { trainer: null, request: null };
        return problem(409, "Coaching.NoTrainer");
      },
    });
    renderSite("/", { access: siteAccess() });
    const card = await myTrainerCard();

    fireEvent.click(await card.findByRole("button", { name: "پایان مربی‌گری" }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "پایان مربی‌گری" }),
    );

    expect(await card.findByRole("alert")).toHaveTextContent("در حال حاضر مربی ندارید.");
    expect(await card.findByText("هنوز مربی ندارید.")).toBeInTheDocument();
  });
});
