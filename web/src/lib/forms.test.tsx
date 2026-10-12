import { fireEvent, render, screen } from "@testing-library/react";
import { useForm, useWatch } from "react-hook-form";

import { withPersianDigits } from "./forms";

/** One number box registered through withPersianDigits, and the value the form holds for it. */
function CountBox() {
  const form = useForm({ defaultValues: { count: "" } });
  const count = useWatch({ control: form.control, name: "count" });

  return (
    <>
      <input aria-label="تعداد" {...withPersianDigits(form.register("count"))} />
      <output aria-label="مقدار فرم">{count}</output>
    </>
  );
}

function typeInto(value: string, caret = value.length) {
  const input = screen.getByLabelText<HTMLInputElement>("تعداد");
  fireEvent.change(input, { target: { value, selectionStart: caret, selectionEnd: caret } });

  return input;
}

describe("withPersianDigits", () => {
  it("withPersianDigits_EnglishDigitsTyped_ShowsAndKeepsPersianDigits", () => {
    render(<CountBox />);

    const input = typeInto("12");

    expect(input).toHaveValue("۱۲");
    expect(screen.getByLabelText("مقدار فرم")).toHaveTextContent("۱۲");
  });

  it("withPersianDigits_ArabicDigitsTyped_ShowsPersianDigits", () => {
    render(<CountBox />);

    expect(typeInto("١٢")).toHaveValue("۱۲");
  });

  it("withPersianDigits_DigitTypedInTheMiddle_KeepsTheCaretThere", () => {
    render(<CountBox />);

    // «۱۲» with a 5 typed between the two digits: the caret stays after the 5.
    const input = typeInto("۱5۲", 2);

    expect(input).toHaveValue("۱۵۲");
    expect(input.selectionStart).toBe(2);
    expect(input.selectionEnd).toBe(2);
  });

  it("withPersianDigits_OtherCharacters_LeftAsTyped", () => {
    render(<CountBox />);

    expect(typeInto("1a")).toHaveValue("۱a");
  });
});
