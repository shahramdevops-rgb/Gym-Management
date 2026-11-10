import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess, trainer } from "../test/renderSite";

const memberNews = {
  items: [
    {
      id: "0199a000-0000-7000-8000-000000000501",
      kind: "EndedByTrainer",
      fullName: "رضا کریمی",
      on: "2026-11-09",
    },
    {
      id: "0199a000-0000-7000-8000-000000000502",
      kind: "Declined",
      fullName: "علی شریفی",
      on: "2026-11-08",
    },
  ],
};

const studentNews = {
  items: [
    {
      id: "0199a000-0000-7000-8000-000000000601",
      kind: "EndedByStudent",
      fullName: "مریم احمدی",
      on: "2026-11-09",
    },
  ],
};

describe("the member's «خبر مربی»", () => {
  it("Home_News_ListsEachWithItsDay", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer/news": () => json(200, memberNews),
    });
    renderSite("/", { access: siteAccess() });

    const list = within(await screen.findByRole("list", { name: "خبر مربی" }));
    const items = list.getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent("رضا کریمی شما را از شاگردانش برداشت.");
    expect(items[0]).toHaveTextContent("۱۴۰۵/۰۸/۱۸");
    expect(items[1]).toHaveTextContent("علی شریفی درخواست شما را نپذیرفت.");
  });

  it("Home_Accepted_SaysTheTrainerAccepted", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer/news": () =>
        json(200, { items: [{ ...memberNews.items[0], kind: "Accepted" }] }),
    });
    renderSite("/", { access: siteAccess() });

    expect(
      await screen.findByText("رضا کریمی درخواست شما را پذیرفت و مربی شما شد."),
    ).toBeInTheDocument();
  });

  it("Home_NoNews_ShowsNoCard", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
    });
    renderSite("/", { access: siteAccess() });

    await waitFor(() =>
      expect(api.requestsTo("GET", "/api/site/coaching/my-trainer/news")).toHaveLength(1),
    );
    expect(screen.queryByRole("heading", { name: "خبر مربی" })).not.toBeInTheDocument();
  });

  it("Seen_SendsTheIdsShownAndTheCardGoes", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer/news": () => json(200, memberNews),
      "POST /api/site/coaching/my-trainer/news/seen": () => json(200, { items: [] }),
    });
    renderSite("/", { access: siteAccess() });

    fireEvent.click(await screen.findByRole("button", { name: "دیدم" }));

    await waitFor(() =>
      expect(screen.queryByRole("heading", { name: "خبر مربی" })).not.toBeInTheDocument(),
    );
    const body = await api
      .requestsTo("POST", "/api/site/coaching/my-trainer/news/seen")[0]!
      .clone()
      .json();
    expect(body).toEqual({ ids: memberNews.items.map((item) => item.id) });
  });

  it("Seen_Refused_ShowsTheErrorAndKeepsTheCard", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/my-trainer/news": () => json(200, memberNews),
      "POST /api/site/coaching/my-trainer/news/seen": () => problem(403, "Coaching.MemberInactive"),
    });
    renderSite("/", { access: siteAccess() });

    fireEvent.click(await screen.findByRole("button", { name: "دیدم" }));

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "خبر مربی" })).toBeInTheDocument();
  });
});

describe("the trainer's «خبر شاگردان»", () => {
  const trainerOnly = () => siteAccess({ member: null, trainer });

  it("Home_StudentEnded_SaysWhoEndedIt", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly().me),
      "GET /api/site/coaching/students/news": () => json(200, studentNews),
    });
    renderSite("/", { access: trainerOnly() });

    const list = within(await screen.findByRole("list", { name: "خبر شاگردان" }));
    expect(list.getByRole("listitem")).toHaveTextContent("مریم احمدی مربی‌گری شما را تمام کرد.");
  });

  it("Seen_SendsTheIdsShown", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly().me),
      "GET /api/site/coaching/students/news": () => json(200, studentNews),
      "POST /api/site/coaching/students/news/seen": () => json(200, { items: [] }),
    });
    renderSite("/", { access: trainerOnly() });

    fireEvent.click(await screen.findByRole("button", { name: "دیدم" }));

    await waitFor(() =>
      expect(screen.queryByRole("heading", { name: "خبر شاگردان" })).not.toBeInTheDocument(),
    );
    const body = await api
      .requestsTo("POST", "/api/site/coaching/students/news/seen")[0]!
      .clone()
      .json();
    expect(body).toEqual({ ids: [studentNews.items[0]!.id] });
  });

  it("Home_TrainerOnly_NeverAsksForTheMembersNews", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly().me),
    });
    renderSite("/", { access: trainerOnly() });

    await waitFor(() =>
      expect(api.requestsTo("GET", "/api/site/coaching/students/news")).toHaveLength(1),
    );
    expect(api.requestsTo("GET", "/api/site/coaching/my-trainer/news")).toHaveLength(0);
  });
});
