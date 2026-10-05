import { fireEvent, screen, within } from "@testing-library/react";

import { jalaliMonthNames, toPersianDigits } from "@/lib/format";

type Box = "روز" | "ماه" | "سال";

/** Opens one of a date field's three boxes and clicks the row with this text. */
export function pickFrom(group: HTMLElement, box: Box, text: string) {
  fireEvent.click(within(group).getByRole("combobox", { name: box }));
  fireEvent.click(within(group).getByRole("option", { name: text }));
}

/** A date field's group of three boxes, found by its label. Looks inside `scope` when given. */
export function dateGroup(label: string, scope?: HTMLElement) {
  return (scope === undefined ? screen : within(scope)).getByRole("group", { name: label });
}

/**
 * Chooses a Jalali date (`1405/08/15`) in a date field's three dropdowns, the way a person would:
 * year, month, day. Looks inside `scope` when given (a dialog), else the page.
 */
export function chooseDate(label: string, date: string, scope?: HTMLElement) {
  const group = dateGroup(label, scope);
  const [year = 0, month = 0, day = 0] = date.split("/").map(Number);

  pickFrom(group, "سال", toPersianDigits(year));
  pickFrom(group, "ماه", jalaliMonthNames[month - 1] ?? "");
  pickFrom(group, "روز", toPersianDigits(day));
}

/** Chooses a birth date (`1370/05/12`) in the «تاریخ تولد» field. */
export function chooseBirthDate(date: string, scope?: HTMLElement) {
  chooseDate("تاریخ تولد", date, scope);
}

/**
 * What a date field shows, in reading order: «۱۰ شهریور ۱۴۰۵», or the boxes' own names
 * («روز ماه سال») while nothing is chosen.
 */
export function dateShown(label: string, scope?: HTMLElement) {
  return within(dateGroup(label, scope))
    .getAllByRole("combobox")
    .map((box) => box.textContent)
    .join(" ");
}
