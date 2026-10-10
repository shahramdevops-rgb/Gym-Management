import { defineConfig } from "@playwright/test";

import { baseURL } from "./e2e/stack";

/**
 * End-to-end tests (task 11.3): `npm run e2e`. Run locally before a release, not in CI: the
 * global setup builds the production images and starts them on https://localhost, which takes
 * minutes. See web/e2e/stack.ts.
 */
export default defineConfig({
  testDir: "./e2e",
  globalSetup: "./e2e/globalSetup.ts",
  globalTeardown: "./e2e/globalTeardown.ts",
  // One stack, one database: the tests share both, so they run one at a time.
  workers: 1,
  fullyParallel: false,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL,
    // Caddy signs "localhost" with its own local authority, which this browser does not know.
    ignoreHTTPSErrors: true,
    // The Chrome already installed on this machine, so there is no browser to download.
    channel: "chrome",
    locale: "fa-IR",
    timezoneId: "Asia/Tehran",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
});
