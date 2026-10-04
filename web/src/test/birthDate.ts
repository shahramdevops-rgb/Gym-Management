import { fireEvent, screen, within } from "@testing-library/react";

import { toPersianDigits } from "@/lib/format";

const monthNames = [
  "فروردین",
  "اردیبهشت",
  "خرداد",
  "تیر",
  "مرداد",
  "شهریور",
  "مهر",
  "آبان",
  "آذر",
  "دی",
  "بهمن",
  "اسفند",
];

/** Opens one of `BirthDateField`'s boxes and clicks the row with this text. */
export function pickFrom(group: HTMLElement, box: "روز" | "ماه" | "سال", text: string) {
  fireEvent.click(within(group).getByRole("combobox", { name: box }));
  fireEvent.click(within(group).getByRole("option", { name: text }));
}

/**
 * Chooses a Jalali birth date (`1370/05/12`) in `BirthDateField`'s three dropdowns, the way a
 * person would: year, month, day. Looks inside `scope` when given (a dialog), else the page.
 */
export function chooseBirthDate(date: string, scope?: HTMLElement) {
  const group = (scope === undefined ? screen : within(scope)).getByRole("group", {
    name: "تاریخ تولد",
  });
  const [year = 0, month = 0, day = 0] = date.split("/").map(Number);

  pickFrom(group, "سال", toPersianDigits(year));
  pickFrom(group, "ماه", monthNames[month - 1] ?? "");
  pickFrom(group, "روز", toPersianDigits(day));
}
