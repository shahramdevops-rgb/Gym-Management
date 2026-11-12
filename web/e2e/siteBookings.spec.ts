import { expect, test, type Browser, type Page } from "@playwright/test";

import { seedTrainer } from "./seed";
import { signInCode, siteURL } from "./stack";

/**
 * Students in the trainer's sessions (roadmap 15.6b) on two phones, against the production stack:
 * the trainer builds a week and a student's «برنامهٔ ثابت», takes the student out of one date and
 * closes another under the warning; the member sees their dates, asks leave from one (the trainer
 * accepts, 15.7i) and moves another.
 * What the unit tests cannot show is the places worked out by the API from the pattern, the fixed
 * schedule and each change, read the same way on both sides.
 */

const trainer = { fullName: "نادر صالحی", typed: "09161112233", stored: "+989161112233" };
const member = { fullName: "نگار شریفی", typed: "09181112233", stored: "+989181112233" };

/** Next week's days: always after tomorrow, so the time of day the test runs at plays no part. */
const days = ["دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه"];

test.use({ viewport: { width: 390, height: 844 }, hasTouch: true });

test.beforeAll(async ({ request }) => {
  await seedTrainer(request, { fullName: trainer.fullName, phoneNumber: trainer.typed });
});

async function shot(page: Page, name: string) {
  const path = test.info().outputPath(`${name}.png`);
  await page.screenshot({ path, fullPage: true });
  await test.info().attach(name, { path, contentType: "image/png" });
}

async function askForCode(page: Page, phone: { typed: string; stored: string }) {
  await page.goto(siteURL);
  await page.getByLabel("شماره موبایل").fill(phone.typed);
  await page.getByRole("button", { name: "دریافت کد" }).click();
  // The code screen first: the code is read from the database only once its request has saved it.
  await expect(page.getByLabel("کد پنج‌رقمی")).toBeVisible();
  await page.getByLabel("کد پنج‌رقمی").fill(signInCode(phone.stored));
}

async function phone(browser: Browser) {
  const context = await browser.newContext({
    ignoreHTTPSErrors: true,
    locale: "fa-IR",
    timezoneId: "Asia/Tehran",
    viewport: { width: 390, height: 844 },
    hasTouch: true,
  });
  return context.newPage();
}

/** The member signs up, asks for the trainer, and the trainer accepts on their own phone (15.4). */
async function linkThem(memberPage: Page, trainerPage: Page) {
  await askForCode(memberPage, member);
  await memberPage.getByLabel("نام و نام خانوادگی").fill(member.fullName);
  await memberPage.getByRole("combobox", { name: "روز" }).click();
  await memberPage.getByRole("option", { name: "۵", exact: true }).click();
  await memberPage.getByRole("combobox", { name: "ماه" }).click();
  await memberPage.getByRole("option", { name: "خرداد", exact: true }).click();
  await memberPage.getByRole("combobox", { name: "سال" }).click();
  await memberPage.getByRole("option", { name: "۱۳۷۵", exact: true }).click();
  await memberPage.getByRole("button", { name: "ثبت‌نام" }).click();
  await memberPage.getByRole("link", { name: "انتخاب مربی" }).click();
  await memberPage
    .getByRole("listitem")
    .filter({ hasText: trainer.fullName })
    .getByRole("button", { name: "انتخاب این مربی" })
    .click();
  await expect(memberPage.getByText(/در انتظار پاسخ است/)).toBeVisible();

  await askForCode(trainerPage, trainer);
  const requests = trainerPage.getByRole("list", { name: "درخواست‌ها" });
  // «پذیرفتن», then the coaching period's 30 days as they are (15.7h).
  await requests.getByRole("button", { name: "پذیرفتن" }).click();
  await requests.getByRole("button", { name: "پذیرفتن" }).click();
  await expect(trainerPage.getByRole("link", { name: "شاگردان من (۱)" })).toBeVisible();
}

/** Two sessions on each of `days`: 13:00 to 14:00, then 14:00 to 15:00, from tomorrow. */
async function buildPattern(page: Page) {
  await page.getByRole("link", { name: "تمام سانس‌های من" }).click();
  await page.getByRole("button", { name: "ساختن الگو" }).click();
  for (const day of days) {
    const region = page.getByRole("region", { name: day });
    await region.getByRole("button", { name: `افزودن سانس به ${day}` }).click();
    await region.getByRole("combobox", { name: "ساعت شروع" }).selectOption("13");
    await region.getByRole("combobox", { name: "ساعت پایان" }).selectOption("14");
    await region.getByRole("button", { name: `افزودن سانس به ${day}` }).click();
  }
  await page.getByRole("button", { name: "ذخیره الگو" }).click();
  await expect(page.getByRole("list", { name: "الگوی هفتگی" })).toBeVisible();
}

/** «دوشنبه ۱۸ آبان»: a day of the week shown, as both sides name its sessions. */
async function labelOf(page: Page, day: string) {
  const text = await page.getByRole("button", { name: new RegExp(`^${day}`) }).textContent();
  // The sessions follow the month's name with no space: «دوشنبه ۱۸ آبان۱۳:۰۰ تا …».
  return text!.match(new RegExp(`^${day} [۰-۹]+ [^۰-۹\\s]+`))![0];
}

test("SiteBookings_TrainerPlacesAStudent_MemberLeavesAndMovesADate", async ({ page, browser }) => {
  const trainerPage = await phone(browser);
  await linkThem(page, trainerPage);
  await buildPattern(trainerPage);

  // Next week's dates, named as the screens name them.
  await trainerPage.getByRole("button", { name: "هفتهٔ بعد" }).click();
  const [monday, tuesday, wednesday, thursday] = await Promise.all(
    days.map((day) => labelOf(trainerPage, day)),
  );

  // The trainer: the student's «برنامهٔ ثابت», 13:00 on each of the four days.
  await trainerPage.getByRole("link", { name: "بازگشت" }).click();
  await trainerPage.getByRole("link", { name: "شاگردان من (۱)" }).click();
  await trainerPage.getByRole("link", { name: `برنامهٔ ثابت ${member.fullName}` }).click();
  await trainerPage.getByRole("button", { name: "ساختن برنامه" }).click();
  for (const day of days) {
    await trainerPage.getByRole("button", { name: `${day} ۱۳:۰۰ تا ۱۴:۰۰` }).click();
  }
  await shot(trainerPage, "1-schedule-editor");
  await trainerPage.getByRole("button", { name: "ذخیره برنامه" }).click();
  await expect(trainerPage.getByRole("list", { name: "سانس‌های هفتگی" })).toContainText(
    "دوشنبه ۱۳:۰۰ تا ۱۴:۰۰",
  );
  // One week at a time, from today: next Monday is a week on.
  await trainerPage.getByRole("button", { name: "هفتهٔ بعد" }).click();
  const places = trainerPage.getByRole("list", { name: "سانس‌های پیش رو" });
  await expect(places).toContainText(monday!);
  await shot(trainerPage, "2-schedule");

  // «شاگردان سانس» of next Monday: the student, taken out of that date only. «بازگشت» leads to
  // «شاگردان من» first, then home.
  await trainerPage.getByRole("link", { name: "بازگشت" }).click();
  await expect(
    trainerPage.getByRole("heading", { name: "شاگردان من (۱)", level: 1 }),
  ).toBeVisible();
  await trainerPage.getByRole("link", { name: "بازگشت" }).click();
  await trainerPage.getByRole("link", { name: "تمام سانس‌های من" }).click();
  await trainerPage.getByRole("button", { name: "هفتهٔ بعد" }).click();
  const mondayButton = trainerPage.getByRole("button", { name: new RegExp(`^${monday}`) });
  await expect(mondayButton).toContainText("۱۳:۰۰ تا ۱۴:۰۰ · ۱ از ۱۰ نفر");
  await mondayButton.click();
  const dialog = trainerPage.getByRole("dialog");
  const session = dialog.getByRole("region", { name: "۱۳:۰۰ تا ۱۴:۰۰" });
  await expect(session).toContainText(member.fullName);
  await shot(trainerPage, "3-session-students");
  await session.getByRole("button", { name: "حذف از این روز" }).click();
  const confirm = trainerPage.getByRole("dialog", { name: "حذف از این روز" });
  await confirm.getByRole("button", { name: "حذف از این روز" }).click();
  await expect(session).toContainText("شاگردی در این سانس نیست.");
  await trainerPage.keyboard.press("Escape");
  await expect(dialog).toBeHidden();

  // Next Tuesday closed under the student: warned first, then saved anyway.
  await trainerPage.getByRole("button", { name: new RegExp(`^${tuesday}`) }).click();
  await dialog.getByRole("button", { name: "تغییر سانس‌های این روز" }).click();
  await dialog.getByRole("button", { name: "تعطیل کردن این روز" }).click();
  await expect(dialog.getByRole("alert")).toContainText("این تغییر به ۱ شاگرد می‌رسد");
  await shot(trainerPage, "4-warning");
  await dialog.getByRole("button", { name: "ذخیره با این حال" }).click();
  await expect(dialog).toBeHidden();
  // The trainer's phone stays open: it answers the leave request below (15.7i).

  // The member: the next dates on the home, then «سانس‌های من».
  await page.reload();
  await expect(page.getByRole("list", { name: "سانس‌های بعدی" })).toBeVisible();
  // The closed Tuesday at the top of the home (15.6c), until «دیدم».
  const cancelled = page.getByRole("list", { name: "سانس‌های لغوشده" });
  await expect(cancelled).toContainText(`${tuesday}۱۳:۰۰ تا ۱۴:۰۰`);
  await shot(page, "5-member-home");
  // «خبر مربی» (15.4d) is on the home too, with its own «دیدم»: this one is the card's.
  await page
    .locator('[data-slot="card"]', { has: cancelled })
    .getByRole("button", { name: "دیدم" })
    .click();
  await expect(cancelled).toBeHidden();
  await page.reload();
  await expect(page.getByRole("list", { name: "سانس‌های بعدی" })).toBeVisible();
  await expect(cancelled).toBeHidden();
  await shot(page, "5b-member-home-seen");
  await page.getByRole("link", { name: "همهٔ سانس‌ها" }).click();
  await page.getByRole("button", { name: "هفتهٔ بعد" }).click();

  const row = (label: string) => page.getByRole("listitem", { name: label });
  await expect(row(`${monday}، ۱۳:۰۰ تا ۱۴:۰۰`)).toHaveCount(0);
  await expect(row(`${tuesday}، ۱۳:۰۰ تا ۱۴:۰۰`)).toContainText("لغو شد");
  await shot(page, "6-my-sessions");

  // Leave by request (15.7i): asked, accepted by the trainer, then taken back with «می‌آیم».
  await row(`${wednesday}، ۱۳:۰۰ تا ۱۴:۰۰`).getByRole("button", { name: "درخواست مرخصی" }).click();
  await page
    .getByRole("dialog", { name: "درخواست مرخصی" })
    .getByRole("button", { name: "درخواست مرخصی" })
    .click();
  const left = row(`${wednesday}، ۱۳:۰۰ تا ۱۴:۰۰`);
  await expect(left).toContainText("در انتظار تأیید مربی");

  await trainerPage.goto(new URL("/", trainerPage.url()).toString());
  const leaveRequests = trainerPage.getByRole("list", { name: "درخواست‌های مرخصی" });
  await expect(leaveRequests).toContainText(member.fullName);
  await shot(trainerPage, "6a-leave-requests");
  await leaveRequests.getByRole("button", { name: "قبول" }).click();
  await expect(leaveRequests).toBeHidden();
  await trainerPage.context().close();

  // A date with leave stays, faded, to take back.
  await page.reload();
  await page.getByRole("button", { name: "هفتهٔ بعد" }).click();
  await expect(left.getByRole("button", { name: "می‌آیم" })).toBeVisible();
  await expect(left).toContainText("مرخصی");
  await shot(page, "6b-left");
  await left.getByRole("button", { name: "می‌آیم" }).click();
  await expect(left.getByRole("button", { name: "درخواست مرخصی" })).toBeVisible();

  await row(`${thursday}، ۱۳:۰۰ تا ۱۴:۰۰`).getByRole("button", { name: "جابه‌جایی" }).click();
  const move = page.getByRole("dialog", { name: "جابه‌جایی" });
  await move.getByRole("button", { name: `${thursday}، ۱۴:۰۰ تا ۱۵:۰۰` }).click();
  await shot(page, "7-move");
  await move.getByRole("button", { name: "جابه‌جایی به این سانس" }).click();
  await expect(move).toBeHidden();
  await expect(row(`${thursday}، ۱۳:۰۰ تا ۱۴:۰۰`)).toHaveCount(0);
  await expect(row(`${thursday}، ۱۴:۰۰ تا ۱۵:۰۰`)).toContainText("جابه‌جا شده");
  await shot(page, "8-moved");
});
