import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";

import { JalaliDateField } from "./FormField";

/**
 * The field is controlled, so the tests drive it through a tiny host that holds the ISO value,
 * exactly as `Controller` does in `MemberForm`.
 */
function Host({ initial = "", error }: { initial?: string; error?: string }) {
  const [value, setValue] = useState(initial);

  return (
    <>
      <JalaliDateField label="تاریخ تولد" error={error} value={value} onChange={setValue} />
      <output data-testid="iso">{value}</output>
    </>
  );
}

const label = "تاریخ تولد";

describe("JalaliDateField", () => {
  it("JalaliDateField_IsoValue_ShowsTheJalaliDate", () => {
    render(<Host initial="1991-08-03" />);

    expect(screen.getByLabelText(label)).toHaveValue("۱۳۷۰/۰۵/۱۲");
  });

  it("JalaliDateField_TypedPersianDigits_EmitsTheIsoDate", () => {
    render(<Host />);

    fireEvent.change(screen.getByLabelText(label), { target: { value: "۱۳۷۰/۰۵/۱۲" } });

    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("JalaliDateField_TypedEnglishDigits_EmitsTheIsoDate", () => {
    render(<Host />);

    fireEvent.change(screen.getByLabelText(label), { target: { value: "1370/5/12" } });

    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("JalaliDateField_HalfTypedDate_EmitsNothingButKeepsTheText", () => {
    render(<Host />);
    const input = screen.getByLabelText(label);

    fireEvent.change(input, { target: { value: "۱۳۷۰/۰۵" } });

    expect(input).toHaveValue("۱۳۷۰/۰۵");
    expect(screen.getByTestId("iso")).toBeEmptyDOMElement();
  });

  it("JalaliDateField_BlurAfterAHalfTypedDate_EmptiesTheBox", () => {
    // Deleting part of a date empties the value straight away, because a form must never hold a
    // date the box no longer shows. Blur makes that visible instead of leaving stray text that
    // looks like it will be saved.
    render(<Host initial="1991-08-03" />);
    const input = screen.getByLabelText(label);

    fireEvent.change(input, { target: { value: "۱۳۷۰/۰۵" } });
    fireEvent.blur(input);

    expect(input).toHaveValue("");
    expect(screen.getByTestId("iso")).toBeEmptyDOMElement();
  });

  it("JalaliDateField_BlurAfterAWholeDate_ShowsItTheOneWayDatesAreWritten", () => {
    render(<Host />);
    const input = screen.getByLabelText(label);

    fireEvent.change(input, { target: { value: "1370/5/12" } });
    fireEvent.blur(input);

    expect(input).toHaveValue("۱۳۷۰/۰۵/۱۲");
    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("JalaliDateField_Clear_EmptiesTheBoxAndEmitsEmpty", () => {
    render(<Host initial="1991-08-03" />);

    fireEvent.click(screen.getByRole("button", { name: "پاک کردن تاریخ" }));

    expect(screen.getByLabelText(label)).toHaveValue("");
    expect(screen.getByTestId("iso")).toBeEmptyDOMElement();
  });

  it("JalaliDateField_Empty_HasNoClearButton", () => {
    render(<Host />);

    expect(screen.queryByRole("button", { name: "پاک کردن تاریخ" })).not.toBeInTheDocument();
  });

  it("JalaliDateField_Error_MarksTheInputInvalidAndPointsAtTheMessage", () => {
    render(<Host error="تاریخ تولد نمی‌تواند در آینده باشد." />);

    const input = screen.getByLabelText(label);
    const message = screen.getByText("تاریخ تولد نمی‌تواند در آینده باشد.");
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAttribute("aria-describedby", message.id);
  });

  it("JalaliDateField_Focus_OpensThePersianCalendar", () => {
    // The only test that proves the picker really mounts with calendar={persian}: a Gregorian
    // one would name the month differently.
    render(<Host initial="1991-08-03" />);

    fireEvent.focus(screen.getByLabelText(label));

    // The month name appears in the header and again in the month list, so count rather than
    // pick: the point is that a Persian month name is on screen at all.
    expect(screen.getAllByText("مرداد").length).toBeGreaterThan(0);
  });
});
