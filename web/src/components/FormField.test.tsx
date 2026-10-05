import { fireEvent, render, screen, within } from "@testing-library/react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";

import { moneyOrNull } from "@/lib/money";
import { chooseBirthDate, chooseDate, pickFrom } from "@/test/jalaliDate";

import { BirthDateField, JalaliCalendarField, JalaliDateField, MoneyField } from "./FormField";

const chequeLabel = "تاریخ چک";

/**
 * The field is controlled, so the tests drive it through a tiny host that holds the ISO value,
 * exactly as `Controller` does in `PayableForm`.
 */
function Host({ initial = "", error }: { initial?: string; error?: string }) {
  const [value, setValue] = useState(initial);

  return (
    <>
      <JalaliDateField label={chequeLabel} error={error} value={value} onChange={setValue} />
      <output data-testid="iso">{value}</output>
    </>
  );
}

describe("JalaliDateField", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("JalaliDateField_IsoValue_ChoosesTheJalaliDayMonthAndYear", () => {
    render(<Host initial="2026-11-06" />); // ۱۵ آبان ۱۴۰۵

    expect(box("روز", chequeLabel)).toHaveTextContent("۱۵");
    expect(box("ماه", chequeLabel)).toHaveTextContent("آبان");
    expect(box("سال", chequeLabel)).toHaveTextContent("۱۴۰۵");
  });

  it("JalaliDateField_AllThreeChosen_EmitsTheIsoDate", () => {
    render(<Host />);

    chooseDate(chequeLabel, "1405/08/15");

    expect(screen.getByTestId("iso")).toHaveTextContent("2026-11-06");
  });

  it("JalaliDateField_Years_RunFromThreeYearsAheadBackTo1404", () => {
    // A cheque can fall due years ahead; nothing in the gym's books is older than ۱۴۰۴.
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-05T08:00:00Z")); // ۱۳ مهر ۱۴۰۵
    render(<Host />);

    expect(rows("سال", chequeLabel)).toEqual(["۱۴۰۸", "۱۴۰۷", "۱۴۰۶", "۱۴۰۵", "۱۴۰۴"]);
  });

  it("JalaliDateField_StoredYearOutsideTheRange_IsStillOffered", () => {
    render(<Host initial="2024-05-01" />); // ۱۲ اردیبهشت ۱۴۰۳

    expect(box("سال", chequeLabel)).toHaveTextContent("۱۴۰۳");
    expect(rows("سال", chequeLabel).at(-1)).toBe("۱۴۰۳");
  });

  it("JalaliDateField_Clear_EmptiesTheBoxesAndEmitsEmpty", () => {
    render(<Host initial="2026-11-06" />);

    fireEvent.click(screen.getByRole("button", { name: `پاک کردن ${chequeLabel}` }));

    expect(box("روز", chequeLabel)).toHaveTextContent("روز");
    expect(box("ماه", chequeLabel)).toHaveTextContent("ماه");
    expect(box("سال", chequeLabel)).toHaveTextContent("سال");
    expect(screen.getByTestId("iso")).toBeEmptyDOMElement();
  });

  it("JalaliDateField_PartlyChosen_CanBeCleared", () => {
    render(<Host />);

    pickFrom(group(chequeLabel), "ماه", "آبان");
    fireEvent.click(screen.getByRole("button", { name: `پاک کردن ${chequeLabel}` }));

    expect(box("ماه", chequeLabel)).toHaveTextContent("ماه");
  });

  it("JalaliDateField_NothingChosen_HasNoClearButton", () => {
    render(<Host />);

    expect(screen.queryByRole("button", { name: /پاک کردن/ })).not.toBeInTheDocument();
  });

  it("JalaliDateField_Error_IsMarkedAndDescribed", () => {
    render(<Host error="تاریخ چک را وارد کنید." />);

    const message = screen.getByText("تاریخ چک را وارد کنید.");
    expect(group(chequeLabel)).toHaveAttribute("aria-describedby", message.id);
    expect(box("سال", chequeLabel)).toHaveAttribute("aria-invalid", "true");
  });
});
/**
 * The field is controlled, so the tests drive it through a tiny host that holds the ISO value,
 * exactly as the history page does.
 */
function CalendarHost({ initial = "", error }: { initial?: string; error?: string }) {
  const [value, setValue] = useState(initial);

  return (
    <>
      <JalaliCalendarField label={calendarLabel} error={error} value={value} onChange={setValue} />
      <output data-testid="iso">{value}</output>
    </>
  );
}

const calendarLabel = "از تاریخ";

describe("JalaliCalendarField", () => {
  it("JalaliCalendarField_IsoValue_ShowsTheJalaliDate", () => {
    render(<CalendarHost initial="1991-08-03" />);

    expect(screen.getByLabelText(calendarLabel)).toHaveValue("۱۳۷۰/۰۵/۱۲");
  });

  it("JalaliCalendarField_TypedPersianDigits_EmitsTheIsoDate", () => {
    render(<CalendarHost />);

    fireEvent.change(screen.getByLabelText(calendarLabel), { target: { value: "۱۳۷۰/۰۵/۱۲" } });

    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("JalaliCalendarField_TypedEnglishDigits_EmitsTheIsoDate", () => {
    render(<CalendarHost />);

    fireEvent.change(screen.getByLabelText(calendarLabel), { target: { value: "1370/5/12" } });

    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("JalaliCalendarField_HalfTypedDate_EmitsNothingButKeepsTheText", () => {
    render(<CalendarHost />);
    const input = screen.getByLabelText(calendarLabel);

    fireEvent.change(input, { target: { value: "۱۳۷۰/۰۵" } });

    expect(input).toHaveValue("۱۳۷۰/۰۵");
    expect(screen.getByTestId("iso")).toBeEmptyDOMElement();
  });

  it("JalaliCalendarField_BlurAfterAHalfTypedDate_EmptiesTheBox", () => {
    // Deleting part of a date empties the value straight away, because a form must never hold a
    // date the box no longer shows. Blur makes that visible instead of leaving stray text that
    // looks like it will be saved.
    render(<CalendarHost initial="1991-08-03" />);
    const input = screen.getByLabelText(calendarLabel);

    fireEvent.change(input, { target: { value: "۱۳۷۰/۰۵" } });
    fireEvent.blur(input);

    expect(input).toHaveValue("");
    expect(screen.getByTestId("iso")).toBeEmptyDOMElement();
  });

  it("JalaliCalendarField_BlurAfterAWholeDate_ShowsItTheOneWayDatesAreWritten", () => {
    render(<CalendarHost />);
    const input = screen.getByLabelText(calendarLabel);

    fireEvent.change(input, { target: { value: "1370/5/12" } });
    fireEvent.blur(input);

    expect(input).toHaveValue("۱۳۷۰/۰۵/۱۲");
    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("JalaliCalendarField_DigitsTypedOneByOne_GetTheirSlashesAndCommit", () => {
    // Typing must work as well as picking: digits alone, keystroke by keystroke, as a person types.
    render(<CalendarHost />);
    const input = screen.getByLabelText(calendarLabel);
    fireEvent.focus(input);

    // Each keystroke adds to what the box shows now, slashes it added included.
    for (const digit of "۱۳۷۰۰۵۱۲") {
      fireEvent.change(input, { target: { value: (input as HTMLInputElement).value + digit } });
    }

    expect(input).toHaveValue("۱۳۷۰/۰۵/۱۲");
    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("JalaliCalendarField_BackspaceOverASlash_DoesNotPutItBack", () => {
    render(<CalendarHost />);
    const input = screen.getByLabelText(calendarLabel);
    fireEvent.change(input, { target: { value: "13700" } });
    expect(input).toHaveValue("1370/0");

    fireEvent.change(input, { target: { value: "1370/" } });
    fireEvent.change(input, { target: { value: "1370" } });

    expect(input).toHaveValue("1370");
  });

  it("JalaliCalendarField_TypedWithDots_IsKeptOnBlur", () => {
    render(<CalendarHost />);
    const input = screen.getByLabelText(calendarLabel);

    fireEvent.change(input, { target: { value: "1370.05.12" } });
    fireEvent.blur(input);

    expect(input).toHaveValue("۱۳۷۰/۰۵/۱۲");
    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("JalaliCalendarField_Clear_EmptiesTheBoxAndEmitsEmpty", () => {
    render(<CalendarHost initial="1991-08-03" />);

    fireEvent.click(screen.getByRole("button", { name: "پاک کردن تاریخ" }));

    expect(screen.getByLabelText(calendarLabel)).toHaveValue("");
    expect(screen.getByTestId("iso")).toBeEmptyDOMElement();
  });

  it("JalaliCalendarField_Empty_HasNoClearButton", () => {
    render(<CalendarHost />);

    expect(screen.queryByRole("button", { name: "پاک کردن تاریخ" })).not.toBeInTheDocument();
  });

  it("JalaliCalendarField_Error_MarksTheInputInvalidAndPointsAtTheMessage", () => {
    render(<CalendarHost error="تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد." />);

    const input = screen.getByLabelText(calendarLabel);
    const message = screen.getByText("تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد.");
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAttribute("aria-describedby", message.id);
  });

  it("JalaliCalendarField_Focus_OpensThePersianCalendar", () => {
    // The only test that proves the picker really mounts with calendar={persian}: a Gregorian
    // one would name the month differently.
    render(<CalendarHost initial="1991-08-03" />);

    fireEvent.focus(screen.getByLabelText(calendarLabel));

    // The month name appears in the header and again in the month list, so count rather than
    // pick: the point is that a Persian month name is on screen at all.
    expect(screen.getAllByText("مرداد").length).toBeGreaterThan(0);
  });

  it("JalaliCalendarField_Focus_OpensTheCalendarInPlaceUnderTheBox", () => {
    // Not a floating popup: inside a scrolling dialog one was clipped and covered the buttons.
    const { container } = render(<CalendarHost initial="1991-08-03" />);
    const input = screen.getByLabelText(calendarLabel);

    fireEvent.focus(input);

    const calendar = container.querySelector(".jalali-calendar");
    expect(calendar).not.toBeNull();
    expect(input).toHaveAttribute("aria-expanded", "true");
    expect(document.getElementById(input.getAttribute("aria-controls")!)).toContainElement(
      calendar as HTMLElement,
    );
  });

  it("JalaliCalendarField_DayPicked_EmitsTheIsoDateAndClosesTheCalendar", () => {
    const { container } = render(<CalendarHost initial="1991-08-03" />);
    const input = screen.getByLabelText(calendarLabel);
    fireEvent.focus(input);

    fireEvent.click(within(container.querySelector(".jalali-calendar")!).getByText("۲۰"));

    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-11");
    expect(input).toHaveValue("۱۳۷۰/۰۵/۲۰");
    expect(container.querySelector(".jalali-calendar")).toBeNull();
  });

  it("JalaliCalendarField_Blur_ClosesTheCalendar", () => {
    const { container } = render(<CalendarHost initial="1991-08-03" />);
    const input = screen.getByLabelText(calendarLabel);
    fireEvent.focus(input);

    fireEvent.blur(input);

    expect(container.querySelector(".jalali-calendar")).toBeNull();
    expect(input).toHaveAttribute("aria-expanded", "false");
  });

  it("JalaliCalendarField_Escape_ClosesTheCalendarAndKeepsTheDate", () => {
    const { container } = render(<CalendarHost initial="1991-08-03" />);
    const input = screen.getByLabelText(calendarLabel);
    fireEvent.focus(input);

    fireEvent.keyDown(input, { key: "Escape" });

    expect(container.querySelector(".jalali-calendar")).toBeNull();
    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
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

function BirthDateHost({ initial = "", error }: { initial?: string; error?: string }) {
  const [value, setValue] = useState(initial);

  return (
    <>
      <BirthDateField label="تاریخ تولد" error={error} value={value} onChange={setValue} />
      <output data-testid="iso">{value}</output>
    </>
  );
}

const label = "تاریخ تولد";

function group(fieldLabel = label) {
  return screen.getByRole("group", { name: fieldLabel });
}

function box(name: "روز" | "ماه" | "سال", fieldLabel = label) {
  return within(group(fieldLabel)).getByRole("combobox", { name });
}

/** The rows a box offers, read with its list open. */
function rows(name: "روز" | "ماه" | "سال", fieldLabel = label) {
  fireEvent.click(box(name, fieldLabel));
  const texts = within(group(fieldLabel))
    .getAllByRole("option")
    .map((option) => option.textContent);
  fireEvent.click(box(name, fieldLabel));
  return texts;
}

describe("BirthDateField", () => {
  it("BirthDateField_IsoValue_ChoosesTheJalaliDayMonthAndYear", () => {
    render(<BirthDateHost initial="1991-08-03" />);

    expect(box("روز")).toHaveTextContent("۱۲");
    expect(box("ماه")).toHaveTextContent("مرداد");
    expect(box("سال")).toHaveTextContent("۱۳۷۰");
  });

  it("BirthDateField_AllThreeChosen_EmitsTheIsoDate", () => {
    render(<BirthDateHost />);

    chooseBirthDate("1370/05/12");

    expect(screen.getByTestId("iso")).toHaveTextContent("1991-08-03");
  });

  it("BirthDateField_PartlyChosen_EmitsNothingButKeepsTheChoices", () => {
    render(<BirthDateHost />);

    pickFrom(group(), "سال", "۱۳۷۰");
    pickFrom(group(), "ماه", "مرداد");

    expect(screen.getByTestId("iso")).toBeEmptyDOMElement();
    expect(box("سال")).toHaveTextContent("۱۳۷۰");
    expect(box("ماه")).toHaveTextContent("مرداد");
    expect(box("روز")).toHaveTextContent("روز");
  });

  it("BirthDateField_Years_RunFrom1400BackTo1320", () => {
    render(<BirthDateHost />);

    const years = rows("سال");

    expect(years[0]).toBe("۱۴۰۰");
    expect(years.at(-1)).toBe("۱۳۲۰");
    expect(years).toHaveLength(81);
  });

  it("BirthDateField_StoredYearOutsideTheRange_IsStillOffered", () => {
    render(<BirthDateHost initial="2023-05-01" />); // ۱۱ اردیبهشت ۱۴۰۲

    expect(box("سال")).toHaveTextContent("۱۴۰۲");
    expect(rows("سال")[0]).toBe("۱۴۰۲");
  });

  it.each([
    ["فروردین", 31],
    ["مهر", 30],
  ])("BirthDateField_Month_OffersItsDays (%s)", (month, days) => {
    render(<BirthDateHost />);

    pickFrom(group(), "ماه", month);

    expect(rows("روز")).toHaveLength(days);
  });

  it.each([
    ["۱۳۹۹", 30],
    ["۱۴۰۰", 29],
  ])("BirthDateField_Esfand_HasThirtyDaysOnlyInALeapYear (%s)", (year, days) => {
    render(<BirthDateHost />);

    pickFrom(group(), "سال", year);
    pickFrom(group(), "ماه", "اسفند");

    expect(rows("روز")).toHaveLength(days);
  });

  it("BirthDateField_DayTheNewMonthLacks_BecomesItsLastDay", () => {
    render(<BirthDateHost initial="1991-04-20" />); // ۳۱ فروردین ۱۳۷۰

    pickFrom(group(), "ماه", "مهر");

    expect(box("روز")).toHaveTextContent("۳۰");
    expect(screen.getByTestId("iso")).toHaveTextContent("1991-10-22"); // ۳۰ مهر ۱۳۷۰
  });

  it("BirthDateField_Keyboard_TypesAYearAndPicksIt", () => {
    render(<BirthDateHost />);
    const year = box("سال");

    fireEvent.keyDown(year, { key: "Enter" });
    for (const digit of "1370") {
      fireEvent.keyDown(year, { key: digit });
    }
    fireEvent.keyDown(year, { key: "Enter" });

    expect(year).toHaveTextContent("۱۳۷۰");
    expect(within(group()).queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("BirthDateField_EscapeInADialog_ClosesTheListOnly", () => {
    const onKeyDown = vi.fn();
    render(
      <div onKeyDown={onKeyDown}>
        <BirthDateHost />
      </div>,
    );

    fireEvent.click(box("روز"));
    fireEvent.keyDown(box("روز"), { key: "Escape" });

    expect(within(group()).queryByRole("listbox")).not.toBeInTheDocument();
    expect(onKeyDown).not.toHaveBeenCalled();
  });

  it("BirthDateField_Chosen_HasNoClearButton", () => {
    render(<BirthDateHost initial="1991-08-03" />);

    expect(screen.queryByRole("button", { name: /پاک کردن/ })).not.toBeInTheDocument();
  });

  it("BirthDateField_Error_IsMarkedAndDescribed", () => {
    render(<BirthDateHost error="تاریخ تولد را وارد کنید." />);

    const message = screen.getByText("تاریخ تولد را وارد کنید.");
    expect(screen.getByLabelText(label)).toHaveAttribute("aria-describedby", message.id);
    expect(box("روز")).toHaveAttribute("aria-invalid", "true");
  });
});
