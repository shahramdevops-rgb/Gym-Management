import { createHmac } from "node:crypto";

import { expect, test, type Page } from "@playwright/test";

import { publicURL, queryValue, siteCodeSecret, siteURL } from "./stack";

/**
 * The members' and trainers' app on its own host (roadmap 15.3b), in the production stack: the
 * same Caddyfile and images a release uses, on https://app.localhost beside the panel's
 * https://localhost.
 */

const phone = { typed: "09351234567", stored: "+989351234567" };

/**
 * The code the fake SMS would have carried. It is never logged or stored (BUSINESS_RULES.md §15),
 * so the test computes it as the API does (HmacSignInCodes): HMAC-SHA256 of the phone and the
 * row's seed under the stack's SITE_CODE_SECRET, its first 8 bytes modulo 100 000.
 */
function signInCode(): string {
  const seed = queryValue(
    `select code_seed from sign_in_codes where phone_number = '${phone.stored}'`,
  );
  const mac = createHmac("sha256", siteCodeSecret).update(`${phone.stored}:${seed}`).digest();

  return (mac.readBigUInt64BE(0) % 100_000n).toString().padStart(5, "0");
}

test("SiteApp_NewPhone_SignsUpWithTheSmsCodeAndStaysSignedIn", async ({ page }) => {
  await page.goto(siteURL);

  await page.getByLabel("شماره موبایل").fill(phone.typed);
  await page.getByRole("button", { name: "دریافت کد" }).click();

  // Sent by itself at the fifth digit.
  await page.getByLabel("کد پنج‌رقمی").fill(signInCode());

  // A phone the gym does not know yet: its name and birth date.
  await page.getByLabel("نام و نام خانوادگی").fill("کاوه رستمی");
  await page.getByRole("combobox", { name: "روز" }).click();
  await page.getByRole("option", { name: "۱۰", exact: true }).click();
  await page.getByRole("combobox", { name: "ماه" }).click();
  await page.getByRole("option", { name: "مهر", exact: true }).click();
  await page.getByRole("combobox", { name: "سال" }).click();
  await page.getByRole("option", { name: "۱۳۷۰", exact: true }).click();
  await page.getByRole("button", { name: "ثبت‌نام" }).click();

  const greeting = page.getByRole("heading", { name: "سلام کاوه رستمی" });
  await expect(greeting).toBeVisible();

  // The refresh cookie lives on the app's host, path /api/site/auth: a reload keeps the session.
  await page.reload();
  await expect(greeting).toBeVisible();
});

test("SiteApp_ProductionStack_ChromeFindsItInstallable", async ({ page }) => {
  await page.goto(siteURL);
  await expect(page.getByRole("button", { name: "دریافت کد" })).toBeVisible();

  const devTools = await page.context().newCDPSession(page);
  const { installabilityErrors } = await devTools.send("Page.getInstallabilityErrors");

  // As for the panel (install.spec.ts): an off-the-record context is the only error allowed.
  const errors = installabilityErrors.map((error) => error.errorId);
  expect(errors.filter((id) => id !== "in-incognito")).toEqual([]);
});

test("SiteApp_ManifestAndServiceWorker_AreServedFreshEveryTime", async ({ page }) => {
  const manifest = (await page.goto(`${siteURL}/manifest.webmanifest`))!;
  expect(manifest.status()).toBe(200);
  expect(manifest.headers()["content-type"]).toContain("application/manifest+json");
  expect((await manifest.json()).short_name).toBe("Pasargad Gym");

  // A release reaches an installed app through a new sw.js: never a cached old one.
  const worker = (await page.goto(`${siteURL}/sw.js`))!;
  expect(worker.status()).toBe(200);
  expect(worker.headers()["content-type"]).toContain("javascript");
  expect(worker.headers()["cache-control"]).toBe("no-cache");
  expect(await worker.text()).toContain("/assets/");
});

/** The status a page's own fetch gets. A string, since the e2e project has no DOM types. */
function statusOf(page: Page, path: string): Promise<number> {
  return page.evaluate(`fetch("${path}").then((response) => response.status)`) as Promise<number>;
}

test("SiteApp_Hosts_ServeOnlyTheirOwnApi", async ({ page }) => {
  // The site's endpoints on the app's host; the staff's are not there at all.
  await page.goto(siteURL);
  expect(await statusOf(page, "/api/site/me")).toBe(401);
  expect(await statusOf(page, "/api/members")).toBe(404);

  // And the other way round: the panel's host has no /api/site.
  await page.goto("/");
  expect(await statusOf(page, "/api/members")).toBe(401);
  expect(await statusOf(page, "/api/site/me")).toBe(404);

  // A hashed file that is not there is a 404, never the app's page.
  expect(await statusOf(page, "/assets/gone-0000.js")).toBe(404);
});

test("PublicPage_QrAddress_RedirectsToTheAppForNow", async ({ page }) => {
  const opened = (await page.goto(`${publicURL}/qr`))!;

  // Temporary, so phones never remember it and the destination can change.
  const redirect = await opened.request().redirectedFrom()!.response();
  expect(redirect!.status()).toBe(302);
  await expect(page).toHaveURL(`${siteURL}/sign-in`);
});

test("PublicPage_SignInButton_OpensTheApp", async ({ page }) => {
  await page.goto(publicURL);

  await page.getByRole("link", { name: "ورود / ثبت‌نام" }).click();

  await expect(page).toHaveURL(`${siteURL}/sign-in`);
  await expect(page.getByRole("button", { name: "دریافت کد" })).toBeVisible();
});
