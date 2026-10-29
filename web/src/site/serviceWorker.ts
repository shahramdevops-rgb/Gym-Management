/**
 * Registers the site app's service worker (site/sw.js, roadmap 15.3b): the installed app then
 * opens without internet, on «ارتباط با سرور برقرار نشد» rather than the browser's error page.
 *
 * Only in a build: in development every file changes all the time, and a copy kept on the phone
 * would only get in the way.
 *
 * Keeping up to date: the browser asks for /sw.js again on each opening of the app (Caddy sends it
 * with no-cache), and a release with any file changed gives a different one, which it installs.
 * An installed app may stay open in the background for days, so the check is also made each time
 * the app comes back to the front.
 */
export function registerServiceWorker(): void {
  if (!import.meta.env.PROD || !("serviceWorker" in navigator)) {
    return;
  }

  window.addEventListener("load", () => {
    navigator.serviceWorker
      .register("/sw.js")
      .then((registration) => {
        document.addEventListener("visibilitychange", () => {
          if (document.visibilityState === "visible") {
            // Offline or mid-release: the next time will do.
            registration.update().catch(() => undefined);
          }
        });
      })
      // No service worker (a private window, an old browser): the app works as a plain page.
      .catch(() => undefined);
  });
}
