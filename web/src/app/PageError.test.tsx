import { screen } from "@testing-library/react";

import type { CurrentlyInside } from "@/features/attendance/api";
import { currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { mockApi, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

describe("PageError", () => {
  it("PageError_PageFailsWhileRendering_ShowsAPersianMessageAndKeepsTheMenu", async () => {
    // A row the page cannot read (no serviceCharges) makes the board throw while rendering.
    const brokenRow: Partial<CurrentlyInside> = insideRow(reza.fullName, openVisit(reza.id));
    delete brokenRow.serviceCharges;
    mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([brokenRow as CurrentlyInside]),
    });
    // The error is logged on purpose; keep it out of the test output.
    vi.spyOn(console, "error").mockImplementation(() => {});

    renderApp("/attendance", { session: session() });

    expect(await screen.findByText(/نمایش این صفحه با خطا روبه‌رو شد/)).toBeInTheDocument();
    // The frame survives, so the desk can go on to another page.
    expect(screen.getByRole("navigation", { name: "منوی اصلی" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "بارگذاری دوباره" })).toBeInTheDocument();
  });
});
