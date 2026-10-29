// The site app follows the phone's own light or dark setting; it has no switch of its own. Run
// before the first paint so a dark phone never sees a white flash. src/site/theme.ts keeps it
// in step when the setting changes while the app is open.
//
// A file of its own, not an inline <script>, for the same reason as the panel's
// (web/public/theme-init.js): the Content-Security-Policy runs only scripts served by the site.
try {
  if (window.matchMedia("(prefers-color-scheme: dark)").matches) {
    document.documentElement.classList.add("dark");
    document.querySelector('meta[name="theme-color"]')?.setAttribute("content", "#0e0f13");
  }
} catch {
  // No media queries: open light.
}
