import { expect, test, type Page } from "@playwright/test";

import { ensureOwnerSignedInOnce, ownerPassword } from "./seed";
import { owner } from "./stack";

/**
 * The shop admin, «مدیر فروشگاه» (task 16.2, ADR 0011), in a real browser against the production
 * stack: the Owner makes the account on «کارمندان», the shop admin changes the temporary password,
 * lands on the shop, fills its contact ways and cannot open a gym page; the Owner then reads the
 * ways and cannot change them.
 *
 * `SHOP_SCREENSHOTS` set to a folder also saves each screen there, to look at after a change.
 */
const shopAdmin = {
  userName: "e2eshop",
  fullName: "مریم فروشگاه",
  temporaryPassword: "shop temporary kettle 1405",
  password: "shop own lantern password 1405",
};

const screenshots = process.env.SHOP_SCREENSHOTS;

async function shot(page: Page, name: string) {
  if (screenshots !== undefined) {
    await page.screenshot({ path: `${screenshots}/${name}.png`, fullPage: true });
  }
}

async function signIn(page: Page, userName: string, password: string) {
  await page.goto("/login");
  await page.getByLabel("نام کاربری").fill(userName);
  await page.getByLabel("رمز عبور", { exact: true }).fill(password);
  await page.getByRole("button", { name: "ورود" }).click();
  // Signed in once the login page is left; going elsewhere before that would land back on it.
  await expect(page).not.toHaveURL(/\/login$/);
}

async function signOut(page: Page) {
  await page.getByRole("button", { name: "خروج از حساب" }).click();
  await expect(page.getByRole("button", { name: "ورود" })).toBeVisible();
}

test.beforeAll(async ({ request }) => {
  await ensureOwnerSignedInOnce(request);
});

test("ShopAdmin_MadeByTheOwner_SeesOnlyTheShopAndTheOwnerReadsIt", async ({ page }) => {
  // The Owner makes the account, choosing «مدیر فروشگاه».
  await signIn(page, owner.userName, ownerPassword);
  await page.goto("/staff");
  await page.getByLabel("نام و نام خانوادگی").fill(shopAdmin.fullName);
  await page.getByLabel("نام کاربری").fill(shopAdmin.userName);
  await page.getByLabel("رمز عبور موقت").fill(shopAdmin.temporaryPassword);
  await page.getByLabel("نقش").selectOption("Shop");
  await shot(page, "1-owner-new-account");
  await page.getByRole("button", { name: "ساخت حساب" }).click();
  await expect(page.getByText(`حساب «${shopAdmin.fullName}» ساخته شد.`)).toBeVisible();
  const row = page.getByRole("row", { name: new RegExp(shopAdmin.fullName) });
  await expect(row.getByText("مدیر فروشگاه")).toBeVisible();
  await shot(page, "2-owner-staff-list");
  await signOut(page);

  // The shop admin's first login: the temporary password is changed first.
  await signIn(page, shopAdmin.userName, shopAdmin.temporaryPassword);
  await page.getByLabel("رمز عبور فعلی").fill(shopAdmin.temporaryPassword);
  await page.getByLabel("رمز عبور جدید", { exact: true }).fill(shopAdmin.password);
  await page.getByLabel("تکرار رمز عبور جدید").fill(shopAdmin.password);
  await page.getByRole("button", { name: "ذخیرهٔ رمز عبور" }).click();

  // Then the shop: its contact ways, and a menu with nothing of the gym's. The panel goes home after
  // the change, and home sends a shop admin to the shop.
  await expect(page).not.toHaveURL(/\/change-password$/);
  await expect(page.getByRole("heading", { name: "راه‌های تماس فروشگاه" })).toBeVisible();
  const menu = page.getByRole("navigation", { name: "منوی اصلی" });
  await expect(menu.getByRole("link", { name: "راه‌های تماس" })).toBeVisible();
  await expect(menu.getByRole("link", { name: "اعضا" })).toHaveCount(0);
  await shot(page, "3-shop-admin-empty");

  await page.getByLabel("تلفن همراه").fill("۰۹۱۲۱۲۳۴۵۶۷");
  await page.getByLabel("تلفن ثابت").fill("۰۷۱۳۲۳۴۵۶۷۸");
  await page.getByLabel("تلگرام").fill("@pasargad_gym");
  await page.getByLabel("اینستاگرام").fill("pasargad.gym");
  await page.getByRole("button", { name: "ذخیره" }).click();
  await expect(page.getByText("تغییرها ثبت شد.")).toBeVisible();
  await shot(page, "4-shop-admin-saved");

  // A gym page typed into the address bar sends them back to the shop.
  await page.goto("/members");
  await expect(page.getByRole("heading", { name: "راه‌های تماس فروشگاه" })).toBeVisible();
  await expect(page).toHaveURL(/\/shop\/contacts$/);
  await signOut(page);

  // The Owner reads the ways and has nothing to save.
  await signIn(page, owner.userName, ownerPassword);
  await page.goto("/shop/contacts");
  await expect(page.getByText("این‌ها را فقط مدیر فروشگاه تغییر می‌دهد.")).toBeVisible();
  await expect(page.getByText("pasargad_gym")).toBeVisible();
  await expect(page.getByRole("button", { name: "ذخیره" })).toHaveCount(0);
  await shot(page, "5-owner-reads");
});
