import { expect, test } from "@playwright/test";

import { siteURL } from "./stack";

/**
 * The members' app opens without internet (roadmap 15.3b, BUSINESS_RULES.md §15): its service
 * worker keeps the app's files, never an answer of the API.
 *
 * A file of its own for one launch option: Chrome refuses a service worker whose script comes with
 * a certificate error, and `ignoreHTTPSErrors` does not reach that check. The stack's certificate is
 * Caddy's local one; the server's real certificate never needs this.
 */
test.use({ launchOptions: { args: ["--ignore-certificate-errors"] } });

test("SiteApp_ServiceWorker_KeepsTheAppForOfflineAndNeverTheApi", async ({ page, context }) => {
  await page.goto(siteURL);
  await expect(page.getByRole("button", { name: "دریافت کد" })).toBeVisible();

  // Strings, since the e2e project has no DOM types; they run in the page. Not waitForFunction:
  // its string is compiled inside the page, which the app's CSP refuses as eval.
  await page.evaluate(`new Promise((resolve) => {
    if (navigator.serviceWorker.controller !== null) resolve(true);
    navigator.serviceWorker.addEventListener("controllerchange", () => resolve(true));
  })`);

  const kept = (await page.evaluate(`(async () => {
    const paths = [];
    for (const name of await caches.keys()) {
      for (const request of await (await caches.open(name)).keys()) {
        paths.push(name + " " + new URL(request.url).pathname);
      }
    }
    return paths;
  })()`)) as string[];
  expect(kept).toContainEqual(expect.stringMatching(/^gym-site-\w+ \/$/));
  expect(kept).toContainEqual(expect.stringMatching(/^gym-site-\w+ \/assets\//));
  expect(kept.filter((path) => path.includes(" /api/"))).toEqual([]);

  // No internet: the app still opens, and says the server cannot be reached.
  await context.setOffline(true);
  await page.reload();
  await expect(page.getByRole("button", { name: "دوباره امتحان کنید" })).toBeVisible();
  await context.setOffline(false);
});
