import { themeBarColour } from "@/lib/theme";

const darkQuery = "(prefers-color-scheme: dark)";

/**
 * The site app follows the phone's light or dark setting (site/public/theme-init.js applies it
 * before the first paint). This follows a change made while the app is open, as when a phone
 * turns dark at sunset. Nothing is stored: the phone already remembers.
 */
export function followSystemTheme(): void {
  const query = window.matchMedia(darkQuery);

  const apply = () => {
    const theme = query.matches ? "dark" : "light";
    document.documentElement.classList.toggle("dark", theme === "dark");
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute("content", themeBarColour[theme]);
  };

  apply();
  query.addEventListener("change", apply);
}
