import { useEffect } from "react";

/**
 * Turns the dark palette on while the calling screen is open, and off when it closes.
 *
 * The class goes on `<html>`, not on the screen's own wrapper: the boxes a screen opens are
 * dialogs rendered into a portal outside it, and they have to be dark too. The page frame (the
 * header and the menu) goes dark with it while the screen is open.
 *
 * Only the locker map uses it for now (BUSINESS_RULES.md §6 *The desk screen's look*). When the
 * whole app goes dark (Phase 13.2), the class moves to `index.html` and this hook goes away.
 */
export function useDarkScreen(): void {
  useEffect(() => {
    const root = document.documentElement;
    root.classList.add("dark");
    return () => root.classList.remove("dark");
  }, []);
}
