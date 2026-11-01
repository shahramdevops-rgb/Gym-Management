import { expect, test, type Page } from "@playwright/test";

import { seedTrainer } from "./seed";
import { signInCode, siteURL } from "./stack";

/**
 * The trainer's «برنامه سانس‌ها» (roadmap 15.6a) on a phone, against the production stack: a week
 * built once, a date of the next week closed on its own, then given back to the pattern. What the
 * unit tests cannot show is the twelve weeks worked out by the API from what was saved.
 */

const trainer = { fullName: "سارا امینی", typed: "09151112233", stored: "+989151112233" };

test.use({ viewport: { width: 390, height: 844 }, hasTouch: true });

test.beforeAll(async ({ request }) => {
  await seedTrainer(request, { fullName: trainer.fullName, phoneNumber: trainer.typed });
});

async function shot(page: Page, name: string) {
  const path = test.info().outputPath(`${name}.png`);
  await page.screenshot({ path, fullPage: true });
  await test.info().attach(name, { path, contentType: "image/png" });
}

/** One session on `day`, 13:00 to 14:00, then the next one, which follows it: 14:00 to 15:00. */
async function addTwoSessions(page: Page, day: string) {
  const region = page.getByRole("region", { name: day });
  await region.getByRole("button", { name: `افزودن سانس به ${day}` }).click();
  await region.getByRole("combobox", { name: "ساعت شروع" }).selectOption("13");
  await region.getByRole("combobox", { name: "ساعت پایان" }).selectOption("14");
  await region.getByRole("button", { name: `افزودن سانس به ${day}` }).click();
}

test("SiteTimetable_TrainerBuildsAWeek_ClosesOneDateAndGivesItBack", async ({ page }) => {
  await page.goto(siteURL);
  await page.getByLabel("شماره موبایل").fill(trainer.typed);
  await page.getByRole("button", { name: "دریافت کد" }).click();
  await page.getByLabel("کد پنج‌رقمی").fill(signInCode(trainer.stored));

  await expect(page.getByText("هنوز برنامهٔ سانسی نساخته‌اید.")).toBeVisible();
  await page.getByRole("link", { name: "برنامه سانس‌ها" }).click();
  await page.getByRole("button", { name: "ساختن الگو" }).click();

  for (const day of ["شنبه", "دوشنبه", "چهارشنبه"]) {
    await addTwoSessions(page, day);
  }
  await shot(page, "1-pattern-editor");
  await page.getByRole("button", { name: "ذخیره الگو" }).click();

  const pattern = page.getByRole("list", { name: "الگوی هفتگی" });
  await expect(pattern.getByRole("listitem").first()).toContainText("۱۳:۰۰ تا ۱۴:۰۰ · ۱۰ نفر");
  await expect(pattern.getByRole("listitem").first()).toContainText("۱۴:۰۰ تا ۱۵:۰۰ · ۱۰ نفر");
  await expect(pattern.getByRole("listitem").nth(1)).toContainText("بدون سانس");
  await shot(page, "2-pattern");

  // A Monday of next week, so the time of day the test runs at plays no part.
  await page.getByRole("button", { name: "هفتهٔ بعد" }).click();
  const monday = page.getByRole("button", { name: /^دوشنبه/ });
  await expect(monday).toContainText("۱۳:۰۰ تا ۱۴:۰۰");

  await monday.click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("شاگردی در این سانس نیست.").first()).toBeVisible();
  await dialog.getByRole("button", { name: "تغییر سانس‌های این روز" }).click();
  await shot(page, "3-day-dialog");
  await dialog.getByRole("button", { name: "تعطیل کردن این روز" }).click();
  await expect(dialog).toBeHidden();
  await expect(monday).toContainText("تعطیل");
  await expect(monday).toContainText("تغییر جداگانه");
  await shot(page, "4-day-closed");

  await monday.click();
  await dialog.getByRole("button", { name: "تغییر سانس‌های این روز" }).click();
  await dialog.getByRole("button", { name: "برگرداندن به الگو" }).click();
  await expect(page.getByRole("dialog")).toBeHidden();
  await expect(monday).toContainText("۱۳:۰۰ تا ۱۴:۰۰");
  await expect(monday).not.toContainText("تغییر جداگانه");

  // Back home: the card shows today's sessions, or says there are none.
  await page.getByRole("link", { name: "بازگشت" }).click();
  await expect(page.getByRole("heading", { name: "سانس‌های امروز" })).toBeVisible();
});
