import { useState } from "react";

import { CheckInOutDialog, type DeskAction } from "./CheckInOutDialog";

/**
 * The check-in and check-out box for a screen: `open` it from a button, render `dialog` once.
 *
 * Each press gets a new `key`, so React throws the previous box's state away and the next one
 * starts at "are you sure?" even for the same member.
 */
export function useDeskDialog() {
  const [current, setCurrent] = useState<{ id: number; action: DeskAction } | null>(null);

  const open = (action: DeskAction) =>
    setCurrent((previous) => ({ id: (previous?.id ?? 0) + 1, action }));

  const dialog =
    current === null ? null : (
      <CheckInOutDialog key={current.id} action={current.action} onClose={() => setCurrent(null)} />
    );

  return { open, dialog };
}
