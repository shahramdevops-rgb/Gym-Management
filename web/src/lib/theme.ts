import { useState } from "react";

export type Theme = "light" | "dark";

/**
 * Where the choice is kept: in this browser, on this device (BUSINESS_RULES.md §14). The script in
 * `public/theme-init.js` reads the same key before the page is drawn, so the two must not drift apart.
 */
export const themeStorageKey = "gym.theme";

/**
 * The saved theme, or light when none is saved. Storage can be blocked (a private window, cleared
 * site data) and then throws; the app simply opens light.
 */
export function readSavedTheme(): Theme {
  try {
    return localStorage.getItem(themeStorageKey) === "dark" ? "dark" : "light";
  } catch {
    return "light";
  }
}

/**
 * Puts the theme on `<html>` and remembers it. The class goes on `<html>`, not on the page frame,
 * because dialogs and the date picker's calendar render in portals outside it.
 */
export function applyTheme(theme: Theme): void {
  document.documentElement.classList.toggle("dark", theme === "dark");
  try {
    localStorage.setItem(themeStorageKey, theme);
  } catch {
    // Not remembered, but the screen still switches; the next visit opens light.
  }
}

/** The current theme and a switch between the two. */
export function useTheme(): { theme: Theme; toggle: () => void } {
  // public/theme-init.js has already applied the saved theme, so <html> is the truth on first render.
  const [theme, setTheme] = useState<Theme>(() =>
    document.documentElement.classList.contains("dark") ? "dark" : "light",
  );

  function toggle() {
    const next = theme === "dark" ? "light" : "dark";
    applyTheme(next);
    setTheme(next);
  }

  return { theme, toggle };
}
