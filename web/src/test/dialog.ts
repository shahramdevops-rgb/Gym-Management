import { act, fireEvent } from "@testing-library/react";

/**
 * Clicks the dimmed backdrop behind the open dialog, the way the desk dismisses a box without
 * hunting for its ×. Radix starts listening for outside presses one tick after the dialog opens,
 * so the helper waits that tick first, and it closes only on the whole press (pointer down, then
 * the click), so the helper sends both.
 */
export async function clickOutsideDialog() {
  await act(() => new Promise((resolve) => setTimeout(resolve, 0)));
  const overlay = document.querySelector('[data-slot="dialog-overlay"]');
  if (overlay === null) {
    throw new Error("No dialog is open.");
  }
  fireEvent.pointerDown(overlay);
  fireEvent.click(overlay);
}
