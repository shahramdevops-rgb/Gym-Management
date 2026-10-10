import { defineConfig } from "@playwright/test";

import config from "./playwright.config";

/**
 * The README's screenshots (roadmap Phase 12): `npm run screenshots`. The same throwaway
 * production stack as the end-to-end tests, filled with the made-up gym of `admin seed-demo`
 * instead of one test member, so nothing from the gym's real data can appear in a picture.
 * Run before each release and commit the new images in docs/screenshots/.
 */
export default defineConfig({
  ...config,
  testDir: "./e2e/screenshots",
  testIgnore: [],
  globalSetup: "./e2e/screenshots/globalSetup.ts",
  // Screenshots are not tests: a picture is never retried, and a failure needs no trace.
  retries: 0,
  reporter: [["list"]],
  use: {
    ...config.use,
    viewport: { width: 1440, height: 900 },
    trace: "off",
    screenshot: "off",
  },
});
