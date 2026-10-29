import { expect, test } from "@playwright/test";

import { seedMemberWithRecords } from "./seed";
import { signInCode, siteURL } from "./stack";

/**
 * A member the desk registered opens the app and sees their own records (roadmap 15.5b), against
 * the production stack: the plan, the debt, the visit with its locker, the payment and their
 * details, as the desk left them; then «خروج» asks before ending the session.
 */

const member = {
  fullName: "نرگس صالحی",
  phoneNumber: "09171112233",
  stored: "+989171112233",
  birthDate: "1992-04-10",
};

test.beforeAll(async ({ request }) => {
  await seedMemberWithRecords(request, member);
});

test("SiteRecords_DeskMember_SeesTheirPlanDebtHistoryAndDetails", async ({ page }) => {
  await page.goto(siteURL);
  await page.getByLabel("شماره موبایل").fill(member.phoneNumber);
  await page.getByRole("button", { name: "دریافت کد" }).click();
  await page.getByLabel("کد پنج‌رقمی").fill(signInCode(member.stored));

  // A phone already in the panel goes straight to the home (§15).
  await expect(page.getByRole("heading", { name: `سلام ${member.fullName}` })).toBeVisible();

  const plan = page.locator("[data-slot='card']", {
    has: page.getByRole("heading", { name: "اشتراک من" }),
  });
  await expect(plan).toContainText("۱۲ جلسه");
  await expect(plan).toContainText("۱۱");
  await expect(plan).toContainText("پرداخت جزئی");

  const debt = page.getByRole("list", { name: "بدهی من" });
  await expect(debt).toContainText("اشتراک ۱۲ جلسه");

  const history = page.locator("[data-slot='card']", {
    has: page.getByRole("heading", { name: "سوابق من" }),
  });
  await expect(history.getByRole("listitem")).toContainText("کمد ۷");
  await history.getByRole("tab", { name: "پرداخت‌ها" }).click();
  await expect(history.getByRole("listitem")).toContainText("کارت");
  await expect(history.getByRole("listitem")).toContainText("۵۰۰٬۰۰۰ تومان");

  await expect(page.getByText("برای اصلاح مشخصات به پذیرش باشگاه مراجعه کنید.")).toBeVisible();

  // «خروج» asks first: signing in again costs an SMS.
  await page.getByRole("button", { name: "خروج" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("یک کد پیامکی تازه لازم است");
  await dialog.getByRole("button", { name: "انصراف" }).click();
  await expect(page.getByRole("heading", { name: `سلام ${member.fullName}` })).toBeVisible();

  await page.getByRole("button", { name: "خروج" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "خروج" }).click();
  await expect(page.getByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeVisible();
});
