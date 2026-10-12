import { expect, test, type Locator, type Page } from "@playwright/test";

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
    expect(await pageWidth(page)).toBeLessThanOrEqual(390);
    await expect(page.getByRole("navigation", { name: "منوی اصلی" })).toBeHidden();
    await expect(page.getByRole("button", { name: "نقشهٔ استفاده" })).toBeHidden();
    await expect(page.getByRole("region", { name: "ورود امروز ساعت به ساعت" })).toHaveCount(0);

    await page.getByRole("button", { name: /^کمد ۱۳، آزاد/ }).tap();
    const checkIn = page.getByRole("dialog");
    await checkIn.getByRole("searchbox", { name: "نام یا شماره موبایل" }).fill("رضا");
    await checkIn.getByRole("button", { name: new RegExp(member.fullName) }).tap();
    await checkIn.getByRole("button", { name: "بله، ورود ثبت شود" }).tap();
    await expect(checkIn.getByText("ورود ثبت شد")).toBeVisible();
    await checkIn.getByText("بستن", { exact: true }).tap();

    const taken = page.getByRole("button", {
      name: new RegExp(`^کمد ۱۳، اشغال — ${member.fullName}`),
    });
    await taken.tap();
    await page.getByRole("dialog").getByRole("button", { name: "ثبت خروج" }).tap();
    const checkOut = page.getByRole("dialog");
    await checkOut.getByLabel("کلید کمد شماره ۱۳ را تحویل گرفتم").check();
    await checkOut.getByRole("button", { name: "بله، خروج ثبت شود" }).tap();
    await expect(checkOut.getByText("کمد شماره ۱۳ آزاد شد.")).toBeVisible();
    await checkOut.getByText("بستن", { exact: true }).tap();
    await expect(page.getByRole("button", { name: /^کمد ۱۳، آزاد/ })).toBeVisible();

    await page.getByRole("button", { name: "منو" }).tap();
    const drawer = page.getByRole("dialog", { name: "منو" });
    await drawer.getByRole("link", { name: "اعضا" }).tap();
    await expect(drawer).toBeHidden();
    await expect(page.getByRole("link", { name: member.fullName })).toBeVisible();
  });

  /**
   * The lists on a phone (roadmap 13.1b): every row is a card, so neither «داخل باشگاه» nor the
   * member list scrolls sideways, check-out works from the card, and the member list's filters
   * share one line.
   */
  test("FrontDesk_Phone_ListsAreCardsAndCheckOutWorksFromOne", async ({ page }) => {
    await signInAsStaff(page);
    await page.getByRole("button", { name: /^کمد ۱۴، آزاد/ }).tap();
    const checkIn = page.getByRole("dialog");
    await checkIn.getByRole("searchbox", { name: "نام یا شماره موبایل" }).fill("رضا");
    await checkIn.getByRole("button", { name: new RegExp(member.fullName) }).tap();
    await checkIn.getByRole("button", { name: "بله، ورود ثبت شود" }).tap();
    await expect(checkIn.getByText("ورود ثبت شد")).toBeVisible();
    await checkIn.getByText("بستن", { exact: true }).tap();

    await page.goto("/attendance");
    const card = page.getByRole("row").filter({ hasText: member.fullName });
    await expect(card).toBeVisible();
    // The headings are the computer's; on a phone each line carries its own.
    await expect(page.getByRole("columnheader", { name: "کمد" })).toBeHidden();
    expect(await pageWidth(page)).toBeLessThanOrEqual(390);

    await card.getByRole("button", { name: "ثبت خروج" }).tap();
    const checkOut = page.getByRole("dialog");
    await checkOut.getByLabel("کلید کمد شماره ۱۴ را تحویل گرفتم").check();
    await checkOut.getByRole("button", { name: "بله، خروج ثبت شود" }).tap();
    await expect(checkOut.getByText("کمد شماره ۱۴ آزاد شد.")).toBeVisible();
    await checkOut.getByText("بستن", { exact: true }).tap();
    await expect(page.getByRole("row").filter({ hasText: member.fullName })).toHaveCount(0);

    await page.goto("/members");
    await expect(page.getByRole("link", { name: member.fullName })).toBeVisible();
    expect(await pageWidth(page)).toBeLessThanOrEqual(390);
    const first = await page.getByRole("button", { name: "همه" }).boundingBox();
    const last = await page.getByRole("button", { name: "پلن تمام‌شده" }).boundingBox();
    expect(last?.y).toBe(first?.y);
  });

  /**
   * Forms on a phone (roadmap 13.1c): a form's box is the whole screen and its buttons are on it
   * without scrolling; an amount is typed on the phone's number pad and a date is chosen by
   * touch.
   */
  test("FrontDesk_Phone_FormsFillTheScreenWithTheirButtonsInReach", async ({ page }) => {
    await signInAsStaff(page);
    await page.getByRole("button", { name: /^کمد ۱۵، آزاد/ }).tap();
    const checkIn = page.getByRole("dialog");
    await expectFullScreen(checkIn);
    // Not on a phone (decided by the developer, 13.1c): only the computer shows today's history.
    await expect(checkIn.getByRole("button", { name: "تاریخچه امروز این کمد" })).toBeHidden();
    await checkIn.getByRole("searchbox", { name: "نام یا شماره موبایل" }).fill("رضا");
    await checkIn.getByRole("button", { name: new RegExp(member.fullName) }).tap();
    await checkIn.getByRole("button", { name: "بله، ورود ثبت شود" }).tap();
    await expect(checkIn.getByText("ورود ثبت شد")).toBeVisible();
    await checkIn.getByText("بستن", { exact: true }).tap();

    await page.getByRole("button", { name: /^کمد ۱۵، اشغال/ }).tap();
    const visit = page.getByRole("dialog");
    await expectFullScreen(visit);
    await expectOnScreen(visit.getByRole("button", { name: "ثبت خروج" }));
    // Moving the locker and its history are the computer's only (13.1c).
    await expect(visit.getByRole("button", { name: "جابه‌جایی کمد" })).toBeHidden();
    await expect(visit.getByRole("button", { name: "تاریخچه امروز این کمد" })).toBeHidden();

    // هوازی's amount in Persian digits, as a phone's keyboard types them.
    await visit.getByRole("button", { name: "هوازی" }).tap();
    const cardio = page.getByRole("dialog", { name: "ثبت مبلغ هوازی" });
    await expectFullScreen(cardio);
    const amount = cardio.getByLabel("مبلغ هوازی");
    await amount.tap();
    await amount.pressSequentially("۱۵۰۰۰۰");
    await expect(amount).toHaveValue("۱۵۰٬۰۰۰");
    await expect(cardio.getByText("صد و پنجاه هزار تومان")).toBeVisible();
    await cardio.getByRole("button", { name: "ثبت", exact: true }).tap();
    await expect(page.getByText("مبلغ هوازی ثبت شد")).toBeVisible();
    await page.getByText("بستن", { exact: true }).tap();

    await visit.getByRole("button", { name: "ثبت خروج" }).tap();
    const checkOut = page.getByRole("dialog");
    await checkOut.getByLabel("کلید کمد شماره ۱۵ را تحویل گرفتم").check();
    await expectOnScreen(checkOut.getByRole("button", { name: "بله، خروج ثبت شود" }));
    await checkOut.getByRole("button", { name: "بله، خروج ثبت شود" }).tap();
    await expect(checkOut.getByText("کمد شماره ۱۵ آزاد شد.")).toBeVisible();
    await checkOut.getByText("بستن", { exact: true }).tap();

    // A birth date, chosen by touch in the new member's form.
    await page.getByRole("button", { name: /^کمد ۱۵، آزاد/ }).tap();
    const register = page.getByRole("dialog");
    await register.getByRole("searchbox", { name: "نام یا شماره موبایل" }).fill("زهره کاظمی");
    await register.getByRole("button", { name: "ثبت این شخص" }).tap();
    await register.getByRole("combobox", { name: "روز" }).tap();
    await page.getByRole("option", { name: "۵", exact: true }).tap();
    await expect(register.getByRole("combobox", { name: "روز" })).toHaveText("۵");
    await expectOnScreen(register.getByRole("button", { name: "ثبت و ادامه" }));
    await register.getByRole("button", { name: "بستن" }).tap();
    await expect(register).toBeHidden();
  });
});

/** A form's box on a phone is the whole screen, and nothing in it scrolls sideways. */
async function expectFullScreen(dialog: Locator) {
  await expect(dialog).toBeVisible();
  // Polled: the box grows in from 96% as it opens, so the first measure can catch it on the way.
  await expect.poll(() => dialog.boundingBox()).toEqual({ x: 0, y: 0, width: 390, height: 844 });
  // A string, since the e2e project has no DOM types; it runs in the page.
  // An expression, not a function: a string is evaluated, never called. The top box is the last.
  expect(
    await dialog
      .page()
      .evaluate(
        "(() => { const box = [...document.querySelectorAll('[role=dialog]')].at(-1); return box.scrollWidth - box.clientWidth; })()",
      ),
  ).toBe(0);
}

/** On the screen as it is, with nothing scrolled. */
async function expectOnScreen(button: Locator) {
  const box = await button.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.y + box!.height).toBeLessThanOrEqual(844);
}

/**
 * How wide the page is drawn, so a test can tell it never scrolls sideways. The page scrolls in
 * the frame's column (the parent of `main`), not the window, so both are measured. A string,
 * since the e2e project has no DOM types; it runs in the page.
 */
async function pageWidth(page: Page): Promise<number> {
  return Number(
    await page.evaluate(
      "Math.max(document.documentElement.scrollWidth, document.querySelector('main').parentElement.scrollWidth)",
    ),
  );
}
