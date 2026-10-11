import { within } from "@testing-library/react";

/**
 * The row of an edit form's save button (`SaveRow`, roadmap 13.2c): every outcome of a save is
 * shown there, beside the button, so a test looks for it there rather than anywhere on the page.
 * The button is found by its label or by «در حال ذخیره…», which it reads while a save is running.
 */
export function saveRowOf(label: string, container: HTMLElement = document.body) {
  const name = new RegExp(`^(${label}|در حال ذخیره…)$`);
  return within(within(container).getByRole("button", { name }).parentElement!);
}

export const nothingChanged = "تغییری نداده‌اید؛ چیزی ذخیره نشد.";
