import { render, screen } from "@testing-library/react";

import { SaveRow } from "./SaveRow";

describe("SaveRow", () => {
  it("SaveRow_NothingToSay_ShowsOnlyTheButtons", () => {
    render(
      <SaveRow>
        <button type="submit">ذخیره</button>
      </SaveRow>,
    );

    expect(screen.getByRole("button", { name: "ذخیره" })).toBeInTheDocument();
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("SaveRow_StaleAndErrorAndNotice_ShowsOnlyTheStaleMessage", () => {
    render(
      <SaveRow error="خطای سرور" notice="saved" stale={<p>کسی دیگر ذخیره کرد</p>}>
        <button type="submit">ذخیره</button>
      </SaveRow>,
    );

    expect(screen.getByText("کسی دیگر ذخیره کرد")).toBeInTheDocument();
    expect(screen.queryByText("خطای سرور")).not.toBeInTheDocument();
    expect(screen.queryByText("تغییرها ثبت شد.")).not.toBeInTheDocument();
  });

  it("SaveRow_ErrorAndNotice_ShowsTheError", () => {
    render(
      <SaveRow error="خطای سرور" notice="unchanged">
        <button type="submit">ذخیره</button>
      </SaveRow>,
    );

    expect(screen.getByRole("alert")).toHaveTextContent("خطای سرور");
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("SaveRow_Saved_SaysSo", () => {
    render(
      <SaveRow notice="saved">
        <button type="submit">ذخیره</button>
      </SaveRow>,
    );

    expect(screen.getByRole("status")).toHaveTextContent("تغییرها ثبت شد.");
  });
});
