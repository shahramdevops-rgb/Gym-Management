import { expect, test } from "@playwright/test";

/**
 * The panel installs as an app of its own (roadmap 13.4). Without a manifest Chrome still offers
 * "add to home screen", but makes a plain shortcut with Chrome's badge on the icon, which opens in
 * a browser tab; nothing on the page itself shows the difference. Chrome's own installability
 * check, through the DevTools protocol, is what tells the two apart.
 */
test("PanelInstall_ProductionStack_ChromeFindsItInstallable", async ({ page }) => {
  await page.goto("/");
  // The manifest is fetched once the page has loaded; the sign-in screen is enough.
  await expect(page.getByRole("button", { name: "ورود" })).toBeVisible();

  const devTools = await page.context().newCDPSession(page);
  const { installabilityErrors } = await devTools.send("Page.getInstallabilityErrors");

  // Playwright's browser contexts are off the record, and Chrome never installs from one; every
  // other check (manifest, icons, secure origin, start URL) is still made and listed.
  const errors = installabilityErrors.map((error) => error.errorId);
  expect(errors.filter((id) => id !== "in-incognito")).toEqual([]);
});

test("PanelInstall_Manifest_IsServedAsManifestJsonAndNotCached", async ({ request }) => {
  const response = await request.get("/manifest.webmanifest");

  expect(response.status()).toBe(200);
  expect(response.headers()["content-type"]).toContain("application/manifest+json");
  expect(response.headers()["cache-control"]).toBe("no-cache");
  expect((await response.json()).short_name).toBe("Management");
});

test("PanelInstall_Icons_AreServedAsPng", async ({ request }) => {
  for (const icon of [
    "/icon-192.png",
    "/icon-512.png",
    "/icon-maskable-512.png",
    "/apple-touch-icon.png",
  ]) {
    const response = await request.get(icon);

    expect(response.status(), icon).toBe(200);
    expect(response.headers()["content-type"], icon).toBe("image/png");
  }
});
