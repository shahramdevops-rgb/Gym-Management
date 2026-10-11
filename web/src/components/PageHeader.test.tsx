import { render, screen } from "@testing-library/react";
import { Coffee, Wallet } from "lucide-react";

import { PageHeader } from "./PageHeader";
import { PageSectionContext } from "./pageSection";

describe("PageHeader", () => {
  it("PageHeader_InsideTheFrame_ShowsTheMenuItemsIconHiddenFromScreenReaders", () => {
    render(
      <PageSectionContext value={{ icon: Coffee, tone: "orange" }}>
        <PageHeader title="بوفه" description="فروش به اعضا و مهمان‌ها" />
      </PageSectionContext>,
    );

    expect(screen.getByTestId("page-header-icon")).toHaveAttribute("aria-hidden", "true");
    // The icon adds nothing to what a screen reader hears: the heading is the title alone.
    expect(screen.getByRole("heading", { level: 2, name: "بوفه" })).toBeInTheDocument();
  });

  it("PageHeader_GroupWithoutAColour_StillShowsTheIcon", () => {
    render(
      <PageSectionContext value={{ icon: Coffee }}>
        <PageHeader title="وضعیت سیستم" />
      </PageSectionContext>,
    );

    expect(screen.getByTestId("page-header-icon")).toBeInTheDocument();
  });

  it("PageHeader_OutsideTheFrame_ShowsNoIcon", () => {
    render(<PageHeader title="ورود" />);

    expect(screen.queryByTestId("page-header-icon")).not.toBeInTheDocument();
  });

  it("PageHeader_OwnIcon_TakesThePlaceOfTheMenuItems", () => {
    render(<PageHeader title="هزینه‌ها" icon={Wallet} />);

    expect(screen.getByTestId("page-header-icon")).toBeInTheDocument();
  });
});
