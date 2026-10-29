import { expect, test, type Page } from "@playwright/test";

import { seedTrainer } from "./seed";
import { signInCode, siteURL } from "./stack";

/**
 * A member picks a trainer and the trainer accepts (roadmap 15.4b), two people on two phones,
 * against the production stack: what the unit tests cannot show is the gym-wide lock, the site's
 * two policies and the two sessions working together.
 */

const trainer = { fullName: "مهدی نوری", typed: "09141112233", stored: "+989141112233" };
const member = { fullName: "لیلا کاظمی", typed: "09131112233", stored: "+989131112233" };

test.beforeAll(async ({ request }) => {
  await seedTrainer(request, { fullName: trainer.fullName, phoneNumber: trainer.typed });
});

async function askForCode(page: Page, phone: { typed: string; stored: string }) {
  await page.goto(siteURL);
  await page.getByLabel("شماره موبایل").fill(phone.typed);
  await page.getByRole("button", { name: "دریافت کد" }).click();
  await page.getByLabel("کد پنج‌رقمی").fill(signInCode(phone.stored));
}

test("SiteCoaching_MemberPicksTrainer_TrainerAcceptsAndSeesTheStudent", async ({
  page,
  browser,
}) => {
  // The member: a new phone signs up, then picks the trainer.
  await askForCode(page, member);
  await page.getByLabel("نام و نام خانوادگی").fill(member.fullName);
  await page.getByRole("combobox", { name: "روز" }).click();
  await page.getByRole("option", { name: "۵", exact: true }).click();
  await page.getByRole("combobox", { name: "ماه" }).click();
  await page.getByRole("option", { name: "خرداد", exact: true }).click();
  await page.getByRole("combobox", { name: "سال" }).click();
  await page.getByRole("option", { name: "۱۳۷۵", exact: true }).click();
  await page.getByRole("button", { name: "ثبت‌نام" }).click();

  await expect(page.getByText("هنوز مربی ندارید.")).toBeVisible();
  await page.getByRole("link", { name: "انتخاب مربی" }).click();
  await page
    .getByRole("listitem")
    .filter({ hasText: trainer.fullName })
    .getByRole("button", { name: "انتخاب این مربی" })
    .click();
  await expect(page.getByText(/در انتظار پاسخ است/)).toContainText(trainer.fullName);

  // The trainer, on their own phone: a phone the Owner added signs straight in.
  const trainerContext = await browser.newContext({
    ignoreHTTPSErrors: true,
    locale: "fa-IR",
    timezoneId: "Asia/Tehran",
  });
  const trainerPage = await trainerContext.newPage();
  await askForCode(trainerPage, trainer);

  const requests = trainerPage.getByRole("list", { name: "درخواست‌ها" });
  await expect(requests).toContainText(member.fullName);
  await requests.getByRole("button", { name: "پذیرفتن" }).click();
  await expect(trainerPage.getByRole("list", { name: "شاگردان من" })).toContainText(
    member.fullName,
  );
  await trainerContext.close();

  // The member sees it when the app is opened again.
  await page.reload();
  await expect(page.getByText(trainer.fullName)).toBeVisible();
  await expect(page.getByRole("button", { name: "پایان مربی‌گری" })).toBeVisible();
});
