import { expect, test, type Page } from "@playwright/test";

import { member, seedFrontDesk, staff } from "./seed";

/**
 * The front desk's whole flow in a real browser against the production stack (roadmap 5.6
 * "Done when", task 11.3): a member of staff signs in, finds a member from a free locker,
 * checks them in, sees the locker taken by them on the board, and checks them out again; then a
 * reload opens a page that downloads when first opened.
 */
test.beforeAll(async ({ request }) => {
  await seedFrontDesk(request);
});

async function signInAsStaff(page: Page) {
  await page.goto("/");
  await page.getByLabel("نام کاربری").fill(staff.userName);
  await page.getByLabel("رمز عبور", { exact: true }).fill(staff.password);
  await page.getByRole("button", { name: "ورود" }).click();
}

test("FrontDesk_SearchCheckInAndCheckOut_LockerFollowsTheVisit", async ({ page }) => {
  await signInAsStaff(page);

  // The board is the desk's first screen.
  await expect(page.getByRole("heading", { name: "ورود با کمد" })).toBeVisible();

  // A free locker opens the search for that locker.
  await page.getByRole("button", { name: /^کمد ۱۲، آزاد/ }).click();
  const checkIn = page.getByRole("dialog");
  await expect(checkIn.getByRole("heading", { name: "کمد شماره ۱۲" })).toBeVisible();
  await checkIn.getByRole("searchbox", { name: "نام یا شماره موبایل" }).fill("رضا");
  await checkIn.getByRole("button", { name: new RegExp(member.fullName) }).click();
  await checkIn.getByRole("button", { name: "بله، ورود ثبت شود" }).click();
  await expect(checkIn.getByText("ورود ثبت شد")).toBeVisible();
  await page.keyboard.press("Escape");

  // The locker now shows who holds it.
  const taken = page.getByRole("button", {
    name: new RegExp(`^کمد ۱۲، اشغال — ${member.fullName}`),
  });
  await expect(taken).toBeVisible();

  // Check-out, taking the key back.
  await taken.click();
  await page.getByRole("dialog").getByRole("button", { name: "ثبت خروج" }).click();
  const checkOut = page.getByRole("dialog");
  await expect(checkOut).toContainText(`آیا از ثبت خروج ${member.fullName} مطمئن هستید؟`);
  await checkOut.getByLabel("کلید کمد شماره ۱۲ را تحویل گرفتم").check();
  await checkOut.getByRole("button", { name: "بله، خروج ثبت شود" }).click();
  await expect(checkOut.getByText("کمد شماره ۱۲ آزاد شد.")).toBeVisible();
  await page.keyboard.press("Escape");

  await expect(page.getByRole("button", { name: /^کمد ۱۲، آزاد/ })).toBeVisible();

  // Straight from the address bar to a page that downloads when first opened: the reload
  // restores the session from the refresh cookie, and the page's own file loads under the CSP.
  await page.goto("/members");
  await expect(page.getByRole("link", { name: member.fullName })).toBeVisible();
});

/**
 * The same desk on a phone (roadmap 13.1a): during a power cut the desk works from one. The map
 * fits the screen with no sideways scroll, a locker is tapped to check a member in and out, and
 * the menu opens as a drawer that closes once a page is chosen.
 */
test.describe("on a phone", () => {
  test.use({ viewport: { width: 390, height: 844 }, hasTouch: true });

  test("FrontDesk_Phone_CheckInAndOutOnTheMapAndTheMenuIsADrawer", async ({ page }) => {
    await signInAsStaff(page);
    await expect(page.getByRole("heading", { name: "ورود با کمد" })).toBeVisible();

    // Nothing wider than the phone, and what is the computer's only is not there
    // (BUSINESS_RULES.md §6).
    // A string, since the e2e project has no DOM types; it runs in the page.
    const pageWidth = Number(await page.evaluate("document.documentElement.scrollWidth"));
    expect(pageWidth).toBeLessThanOrEqual(390);
    await expect(page.getByRole("navigation", { name: "منوی اصلی" })).toBeHidden();
    await expect(page.getByRole("button", { name: "نقشهٔ استفاده" })).toBeHidden();
    await expect(page.getByRole("region", { name: "ورود امروز ساعت به ساعت" })).toHaveCount(0);

    await page.getByRole("button", { name: /^کمد ۱۳، آزاد/ }).tap();
    const checkIn = page.getByRole("dialog");
    await checkIn.getByRole("searchbox", { name: "نام یا شماره موبایل" }).fill("رضا");
    await checkIn.getByRole("button", { name: new RegExp(member.fullName) }).tap();
    await checkIn.getByRole("button", { name: "بله، ورود ثبت شود" }).tap();
    await expect(checkIn.getByText("ورود ثبت شد")).toBeVisible();
    await checkIn.getByRole("button", { name: "بستن" }).tap();

    const taken = page.getByRole("button", {
      name: new RegExp(`^کمد ۱۳، اشغال — ${member.fullName}`),
    });
    await taken.tap();
    await page.getByRole("dialog").getByRole("button", { name: "ثبت خروج" }).tap();
    const checkOut = page.getByRole("dialog");
    await checkOut.getByLabel("کلید کمد شماره ۱۳ را تحویل گرفتم").check();
    await checkOut.getByRole("button", { name: "بله، خروج ثبت شود" }).tap();
    await expect(checkOut.getByText("کمد شماره ۱۳ آزاد شد.")).toBeVisible();
    await checkOut.getByRole("button", { name: "بستن" }).tap();
    await expect(page.getByRole("button", { name: /^کمد ۱۳، آزاد/ })).toBeVisible();

    await page.getByRole("button", { name: "منو" }).tap();
    const drawer = page.getByRole("dialog", { name: "منو" });
    await drawer.getByRole("link", { name: "اعضا" }).tap();
    await expect(drawer).toBeHidden();
    await expect(page.getByRole("link", { name: member.fullName })).toBeVisible();
  });
});
