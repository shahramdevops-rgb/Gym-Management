import { render, screen } from "@testing-library/react";

import { NewBadge } from "./NewBadge";

describe("NewBadge", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("NewBadge_BeforeTheDay_ShowsJadid", () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-11-20T19:00:00Z")); // ۲۹ آبان، ۲۲:۳۰ در تهران

    render(<NewBadge until="2026-11-21" />);

    expect(screen.getByText("جدید")).toBeInTheDocument();
  });

  it("NewBadge_OnTheDayInTehran_ShowsNothing", () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    // Still 20 November in UTC, but 21 November, 00:30, in the gym's time zone.
    vi.setSystemTime(new Date("2026-11-20T21:00:00Z"));

    const { container } = render(<NewBadge until="2026-11-21" />);

    expect(container).toBeEmptyDOMElement();
  });

  it("NewBadge_Shown_IsHiddenFromScreenReaders", () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-11-14T08:00:00Z"));

    render(<NewBadge until="2026-11-21" />);

    expect(screen.getByText("جدید")).toHaveAttribute("aria-hidden", "true");
  });
});
