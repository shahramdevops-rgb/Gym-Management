import { cleanup, render, screen } from "@testing-library/react";

import { Dialog, DialogContent, DialogTitle } from "./dialog";
import { dialogActionsClassName } from "./dialogActions";

function renderDialog(fullScreenOnPhone?: boolean) {
  render(
    <Dialog open>
      <DialogContent fullScreenOnPhone={fullScreenOnPhone} aria-describedby={undefined}>
        <DialogTitle>فروش</DialogTitle>
      </DialogContent>
    </Dialog>,
  );

  return screen.getByRole("dialog");
}

/** The classes `after` has that `before` does not. */
function addedClasses(before: string[], after: Element): string[] {
  const had = new Set(before);
  return [...after.classList].filter((name) => !had.has(name));
}

describe("Dialog", () => {
  // A form fills a phone's screen (13.1c); the computer's centred card stays as it was, so
  // whatever `fullScreenOnPhone` adds is a `max-lg:` rule.
  it("DialogContent_FullScreenOnPhone_ChangesThePhoneOnly", () => {
    const card = [...renderDialog().classList];
    cleanup();

    const full = renderDialog(true);

    const added = addedClasses(card, full);
    expect(added).toContain("max-lg:h-dvh");
    for (const name of added) {
      expect(name).toMatch(/^max-lg:/);
    }
  });

  it("DialogContent_FullScreenOnPhone_MarksItselfForItsButtonRow", () => {
    expect(renderDialog(true)).toHaveAttribute("data-phone-full");
  });

  // A short confirmation stays a card on a phone (decided by the developer, 13.1c).
  it("DialogContent_ByDefault_StaysACard", () => {
    const dialog = renderDialog();

    expect(dialog).not.toHaveAttribute("data-phone-full");
    expect([...dialog.classList]).not.toContain("max-lg:h-dvh");
  });

  // The row is shared with forms on pages and on the computer, where it must do nothing.
  it("DialogActions_EveryClass_AppliesOnlyInAFullScreenDialogOnAPhone", () => {
    const names = dialogActionsClassName.split(" ");

    expect(names).toContain("max-lg:in-data-phone-full:sticky");
    for (const name of names) {
      expect(name).toMatch(/^max-lg:in-data-phone-full:/);
    }
  });
});
