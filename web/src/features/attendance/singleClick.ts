import type { MouseEvent } from "react";

/**
 * Drops a click that is the second or third of a double or triple click (`detail` above 1). Each
 * step of a desk box puts its main button about where the last one was, so a nervous double click
 * on a member would land its second click on «بله، ورود ثبت شود», and a third on the single visit
 * the refusal offers (found in the UX review, 1405/07/21). A deliberate second click, a moment
 * later, starts a new count and goes through; so do Enter and Space, whose `detail` is 0.
 */
export function singleClick(handler: () => void) {
  return (event: MouseEvent<HTMLButtonElement>) => {
    if (event.detail > 1) {
      return;
    }
    handler();
  };
}
