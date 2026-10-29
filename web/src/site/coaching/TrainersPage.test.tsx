import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import { activeMember, renderSite, siteAccess } from "../test/renderSite";

const reza = { trainerId: "0199a000-0000-7000-8000-000000000201", fullName: "رضا کریمی" };
const sara = { trainerId: "0199a000-0000-7000-8000-000000000202", fullName: "سارا احمدی" };
const mina = { trainerId: "0199a000-0000-7000-8000-000000000203", fullName: "مینا رضایی" };

const changing = {
  trainer: { ...reza, since: "2026-10-20" },
  request: {
    requestId: "0199a000-0000-7000-8000-000000000301",
    ...sara,
    requestedOn: "2026-10-29",
  },
};

function row(name: string) {
  const item = screen.getByText(name).closest("li");
  if (item === null) {
    throw new Error(`${name} is not on the list`);
  }
  return within(item);
}

describe("TrainersPage", () => {
  it("Trainers_ChangingTrainer_MarksTheTrainerAndTheOneAsked", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/trainers": () => json(200, [reza, sara, mina]),
      "GET /api/site/coaching/my-trainer": () => json(200, changing),
    });

    renderSite("/trainers", { access: siteAccess() });

    await screen.findByRole("list", { name: "مربیان" });
    expect(row("رضا کریمی").getByText("مربی شما")).toBeInTheDocument();
    expect(row("سارا احمدی").getByText("در انتظار پاسخ")).toBeInTheDocument();
    expect(row("مینا رضایی").getByRole("button", { name: "انتخاب این مربی" })).toBeEnabled();
    expect(
      screen.getByText("تا پذیرفتن مربی تازه، رضا کریمی مربی شما می‌ماند."),
    ).toBeInTheDocument();
  });

  it("Trainers_Pick_SendsTheRequestAndReturnsHome", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/trainers": () => json(200, [reza, mina]),
      "GET /api/site/coaching/my-trainer": () => json(200, { trainer: null, request: null }),
      "POST /api/site/coaching/my-trainer/request": () =>
        json(200, {
          trainer: null,
          request: {
            requestId: "0199a000-0000-7000-8000-000000000302",
            ...mina,
            requestedOn: "2026-10-29",
          },
        }),
    });
    renderSite("/trainers", { access: siteAccess() });
    await screen.findByRole("list", { name: "مربیان" });

    fireEvent.click(row("مینا رضایی").getByRole("button", { name: "انتخاب این مربی" }));

    expect(await screen.findByRole("heading", { name: "مربی من" })).toBeInTheDocument();
    expect(await screen.findByText(/در انتظار پاسخ است/)).toHaveTextContent("مینا رضایی");
    const [sent] = api.requestsTo("POST", "/api/site/coaching/my-trainer/request");
    expect(await sent!.json()).toEqual({ trainerId: mina.trainerId });
  });

  it("Trainers_FiveRequestsToday_SaysToTryTomorrow", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/trainers": () => json(200, [reza]),
      "GET /api/site/coaching/my-trainer": () => json(200, { trainer: null, request: null }),
      "POST /api/site/coaching/my-trainer/request": () =>
        problem(422, "Coaching.TooManyRequestsToday"),
    });
    renderSite("/trainers", { access: siteAccess() });
    await screen.findByRole("list", { name: "مربیان" });

    fireEvent.click(row("رضا کریمی").getByRole("button", { name: "انتخاب این مربی" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("فردا دوباره امتحان کنید");
    expect(screen.getByRole("heading", { name: "انتخاب مربی" })).toBeInTheDocument();
  });

  it("Trainers_DeactivatedMember_IsSentHome", async () => {
    const inactive = { member: { ...activeMember, isActive: false }, trainer: null };
    mockApi({
      "GET /api/site/me": () => json(200, inactive),
      "GET /api/site/coaching/trainers": () => problem(403, "Coaching.MemberInactive"),
      "GET /api/site/coaching/my-trainer": () => problem(403, "Coaching.MemberInactive"),
    });

    renderSite("/trainers", { access: siteAccess(inactive) });

    expect(await screen.findByRole("alert")).toHaveTextContent("عضویت شما در باشگاه غیرفعال است");
    expect(screen.queryByRole("heading", { name: "انتخاب مربی" })).not.toBeInTheDocument();
  });
});
