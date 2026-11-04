import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { json, mockApi } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess } from "../test/renderSite";

const latest = "2026-11-03T18:30:00.123456+00:00";

const cancelled = {
  sessions: [
    { date: "2026-11-07", start: "13:00:00", end: "14:00:00" },
    { date: "2026-11-14", start: "13:00:00", end: "14:00:00" },
  ],
  latest,
};

describe("the member's «سانس لغو شد»", () => {
  it("Home_SessionsCancelled_ListsThemAtTheTop", async () => {
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions/cancelled": () => json(200, cancelled),
    });
    renderSite("/", { access: siteAccess() });

    const list = within(await screen.findByRole("list", { name: "سانس‌های لغوشده" }));
    expect(list.getAllByRole("listitem")).toHaveLength(2);
    expect(list.getAllByRole("listitem")[0]).toHaveTextContent("شنبه ۱۶ آبان۱۳:۰۰ تا ۱۴:۰۰");
    expect(screen.getByText("مربی‌تان این سانس‌ها را لغو کرد.")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 2 })[0]).toHaveTextContent("سانس لغو شد");
  });

  it("Home_NothingCancelled_ShowsNoCard", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
    });
    renderSite("/", { access: siteAccess() });

    await waitFor(() =>
      expect(api.requestsTo("GET", "/api/site/me/sessions/cancelled")).toHaveLength(1),
    );
    expect(screen.queryByRole("heading", { name: "سانس لغو شد" })).not.toBeInTheDocument();
  });

  it("Seen_SendsTheLatestReadAndTheCardGoes", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/me/sessions/cancelled": () => json(200, cancelled),
      "POST /api/site/me/sessions/cancelled/seen": () => json(200, { sessions: [], latest: null }),
    });
    renderSite("/", { access: siteAccess() });

    fireEvent.click(await screen.findByRole("button", { name: "دیدم" }));

    await waitFor(() =>
      expect(screen.queryByRole("heading", { name: "سانس لغو شد" })).not.toBeInTheDocument(),
    );
    const body = await api
      .requestsTo("POST", "/api/site/me/sessions/cancelled/seen")[0]!
      .clone()
      .json();
    expect(body).toEqual({ upTo: latest });
  });
});
