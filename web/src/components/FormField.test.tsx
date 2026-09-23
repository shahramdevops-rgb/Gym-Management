import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";

import { moneyOrNull } from "@/lib/money";

import { JalaliDateField, MoneyField } from "./FormField";

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

/**
 * The money field is controlled too, so it is driven through a real react-hook-form, the way
 * every form in the app uses it: `Controller` holds the text and the submit handler turns it
 * into what the API is sent.
 */
function MoneyHost({
  initial = "",
  error,
  optional = false,
  onSubmitted,
}: {
  initial?: string;
  error?: string;
  optional?: boolean;
  onSubmitted?: (amount: string | null) => void;
}) {
  const form = useForm({ defaultValues: { amount: initial } });

  return (
    <form
      onSubmit={form.handleSubmit((values) => {
        onSubmitted?.(optional ? moneyOrNull(values.amount) : values.amount);
      })}
      noValidate
    >
      <Controller
        control={form.control}
        name="amount"
        render={({ field }) => (
          <MoneyField
            label={moneyLabel}
            error={error}
            optional={optional}
            name={field.name}
            value={field.value}
            onChange={field.onChange}
            onBlur={field.onBlur}
          />
        )}
      />
      <button type="submit">ثبت</button>
    </form>
  );
}

const moneyLabel = "مبلغ (تومان)";

describe("MoneyField", () => {
  it("MoneyField_Typing_GroupsTheDigitsInThrees", () => {
    render(<MoneyHost />);
    const input = screen.getByLabelText(moneyLabel);

    fireEvent.change(input, { target: { value: "900000" } });

    expect(input).toHaveValue("۹۰۰٬۰۰۰");
  });

  it("MoneyField_Typing_ShowsTheAmountInWordsUnderTheInput", () => {
    render(<MoneyHost />);

    fireEvent.change(screen.getByLabelText(moneyLabel), { target: { value: "500000" } });

    expect(screen.getByText("پانصد هزار تومان")).toBeInTheDocument();
  });

  it("MoneyField_Words_AreReadWithTheInput", () => {
    // The words are the check against a miscounted zero, so they belong to the input for a
    // screen reader as much as for an eye.
    render(<MoneyHost initial="500000" />);

    const input = screen.getByLabelText(moneyLabel);
    const words = screen.getByText("پانصد هزار تومان");
    expect(input.getAttribute("aria-describedby")).toContain(words.id);
  });

  it("MoneyField_ValueFromTheForm_IsGroupedAndSpeltOutBeforeAnythingIsTyped", () => {
    // An edit form loads a price as a bare 1500000; it must not be shown as a raw run of zeros.
    render(<MoneyHost initial="1500000" />);

    expect(screen.getByLabelText(moneyLabel)).toHaveValue("۱٬۵۰۰٬۰۰۰");
    expect(screen.getByText("یک میلیون و پانصد هزار تومان")).toBeInTheDocument();
  });

  it("MoneyField_PersianDigitsAndSeparators_ReadBackTheSame", () => {
    render(<MoneyHost />);
    const input = screen.getByLabelText(moneyLabel);

    fireEvent.change(input, { target: { value: "۱٬۵۰۰٬۰۰۰" } });

    expect(input).toHaveValue("۱٬۵۰۰٬۰۰۰");
    expect(screen.getByText("یک میلیون و پانصد هزار تومان")).toBeInTheDocument();
  });

  it("MoneyField_Letters_NeverReachTheBox", () => {
    render(<MoneyHost />);
    const input = screen.getByLabelText(moneyLabel);

    fireEvent.change(input, { target: { value: "12ابج3" } });

    expect(input).toHaveValue("۱۲۳");
  });

  it("MoneyField_Empty_ShowsNoWords", () => {
    render(<MoneyHost />);

    // A words line always describes the input, so no description means no line — and in
    // particular not «صفر تومان», which an empty box is not.
    expect(screen.getByLabelText(moneyLabel)).not.toHaveAttribute("aria-describedby");
    expect(screen.queryByText("صفر تومان")).not.toBeInTheDocument();
  });

  it("MoneyField_EmptyAndOptional_SaysSoInsteadOfGoingQuiet", () => {
    render(<MoneyHost optional />);

    expect(screen.getByText("بدون مبلغ")).toBeInTheDocument();
  });

  it("MoneyField_EmptyOptionalAmount_SubmitsAsNull", async () => {
    const submitted = vi.fn();
    render(<MoneyHost optional onSubmitted={submitted} />);

    fireEvent.click(screen.getByRole("button", { name: "ثبت" }));

    await vi.waitFor(() => expect(submitted).toHaveBeenCalledWith(null));
  });

  it("MoneyField_Error_MarksTheInputInvalidAndPointsAtTheMessage", () => {
    render(<MoneyHost error="مبلغ را وارد کنید." />);

    const input = screen.getByLabelText(moneyLabel);
    const message = screen.getByText("مبلغ را وارد کنید.");
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input.getAttribute("aria-describedby")).toContain(message.id);
  });
});
