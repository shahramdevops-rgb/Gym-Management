// The site app's service worker (roadmap 15.3b): what lets the installed app open without
// internet and keep itself up to date. It is a template: vite.site.config.ts writes the build's
// file list and a version into it and emits it as dist-site/sw.js, so every release that changes
// a file is a new service worker, which the phone installs by itself (src/site/serviceWorker.ts).
//
// What it does, and nothing more:
// - /api is never touched: codes, sessions and a member's data always go to the server.
// - /assets/* (names that carry their content's hash) come from the phone's copy, made when this
//   version was installed.
// - Everything else (the page, theme-init.js, the icons) is asked from the server first; with no
//   internet the copy is used, and the page itself then shows «ارتباط با سرور برقرار نشد»
//   (15.3a's `unreachable`) instead of the browser's own error page.
// The program offline (15.11) grows from here.

const version = "__VERSION__";
const assets = ["__ASSETS__"];

const cacheName = `gym-site-${version}`;
const shell = "/";

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(cacheName)
      // "reload": past the browser's own HTTP cache, so the copy is this release's.
      .then((cache) =>
        cache.addAll(
          [shell, "/theme-init.js", ...assets].map((url) => new Request(url, { cache: "reload" })),
        ),
      )
      // A new version takes over at once rather than when every tab of the old one is closed:
      // an installed app is rarely closed. The open page keeps its own files; the next opening
      // is the new release. A file the old page asks for later and no longer exists makes it
      // reload (src/lib/staleChunkReload.ts).
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((names) =>
        Promise.all(
          names
            .filter((name) => name.startsWith("gym-site-") && name !== cacheName)
            .map((name) => caches.delete(name)),
        ),
      )
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  const url = new URL(request.url);

  if (
    request.method !== "GET" ||
    url.origin !== self.location.origin ||
    url.pathname.startsWith("/api/")
  ) {
    return;
  }

  if (url.pathname.startsWith("/assets/")) {
    event.respondWith(caches.match(request).then((copy) => copy ?? fetch(request)));
    return;
  }

  event.respondWith(
    fetch(request).catch(async () => {
      // Every address of the app is the same page (/sign-in, /sign-up): the router reads it.
      const copy = await caches.match(request.mode === "navigate" ? shell : request);
      return copy ?? Response.error();
    }),
  );
});
