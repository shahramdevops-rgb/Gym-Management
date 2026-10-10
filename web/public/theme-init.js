// The saved theme goes on before anything is drawn, so a dark choice never flashes white on load.
// Same key as src/lib/theme.ts; blocked storage just leaves the page light.
//
// A file of its own, not an inline <script> in index.html: the panel's Content-Security-Policy
// (deploy/Caddyfile) runs only scripts served from the panel itself, and an inline one would need
// 'unsafe-inline', which is exactly what lets an injected script run (task 11.2). It stays a
// classic, render-blocking script, so it still runs before the first paint.
try {
  if (localStorage.getItem("gym.theme") === "dark") {
    document.documentElement.classList.add("dark");
  }
} catch {
  // Storage blocked: open light.
}
