import { readFileSync } from "node:fs";
import { request as httpsRequest } from "node:https";

import { expect, test, type APIRequestContext, type Locator, type Page } from "@playwright/test";

import { ensureOwnerSignedInOnce, ownerPassword } from "./seed";
import { owner, publicURL, siteURL } from "./stack";

/**
 * The shop admin, «مدیر فروشگاه» (task 16.2, ADR 0011), in a real browser against the production
 * stack: the Owner makes the account on «کارمندان», the shop admin changes the temporary password,
 * lands on the shop, fills its contact ways, cannot open a gym page, and adds a category, a product
 * and a photo (16.3); the Owner then reads the ways and cannot change them. Then the showcase
 * (16.4): the gym's public address shows the shop's products to anyone, on the page's «فروشگاه»
 * section and under /shop/, and reaches nothing else of the API.
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

/** One part of a long page (the public page's «فروشگاه» section) rather than the whole of it. */
async function shotOf(part: Locator, name: string) {
  if (screenshots !== undefined) {
    await part.screenshot({ path: `${screenshots}/${name}.png` });
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

  // Then the shop: its products (the shop's home since 16.3), and a menu with nothing of the gym's.
  // The panel goes home after the change, and home sends a shop admin to the shop.
  await expect(page).not.toHaveURL(/\/change-password$/);
  await expect(page.getByRole("heading", { name: "کالاهای فروشگاه" })).toBeVisible();
  const menu = page.getByRole("navigation", { name: "منوی اصلی" });
  await expect(menu.getByRole("link", { name: "راه‌های تماس" })).toBeVisible();
  await expect(menu.getByRole("link", { name: "اعضا" })).toHaveCount(0);
  await shot(page, "3-shop-admin-empty");

  await page.goto("/shop/contacts");

  await page.getByLabel("تلفن همراه").fill("۰۹۱۲۱۲۳۴۵۶۷");
  await page.getByLabel("تلفن ثابت").fill("۰۷۱۳۲۳۴۵۶۷۸");
  await page.getByLabel("تلگرام").fill("@pasargad_gym");
  await page.getByLabel("اینستاگرام").fill("pasargad.gym");
  await page.getByRole("button", { name: "ذخیره" }).click();
  await expect(page.getByText("تغییرها ثبت شد.")).toBeVisible();
  await shot(page, "4-shop-admin-saved");

  // A gym page typed into the address bar sends them back to the shop.
  await page.goto("/members");
  await expect(page.getByRole("heading", { name: "کالاهای فروشگاه" })).toBeVisible();
  await expect(page).toHaveURL(/\/shop\/products$/);

  // A category and a product, then a photo through the real stack: the API re-encodes it and writes
  // it to the files volume, which only works if the image gave the API's user that folder (16.3).
  await page.goto("/shop/categories");
  await page.getByLabel("دستهٔ تازه").fill("مکمل");
  await page.getByRole("button", { name: "افزودن" }).click();
  await expect(page.getByText("دستهٔ «مکمل» اضافه شد.")).toBeVisible();
  await page.goto("/shop/products/new");
  await page.getByLabel("نام کالا").fill("پروتئین وی");
  await page.getByLabel("دسته").selectOption({ label: "مکمل" });
  await page.getByLabel("قیمت (تومان)").fill("۱۲۵۰۰۰۰");
  await page.getByLabel("موجودی").fill("۶");
  await page.getByRole("button", { name: "ذخیره و افزودن عکس" }).click();
  await expect(page.getByText("کالا ذخیره شد. حالا عکس‌هایش را اضافه کنید.")).toBeVisible();
  await page.getByLabel("افزودن عکس").setInputFiles("public-site/img/shop-protein.webp");
  const photo = page.getByAltText("عکس ۱ از پروتئین وی");
  await expect(photo).toBeVisible();
  // The file itself, from the volume, as anyone may read it: a WebP, not a broken image.
  const source = await photo.getAttribute("src");
  const file = await page.request.get(source!);
  expect(file.status()).toBe(200);
  expect(file.headers()["content-type"]).toBe("image/webp");
  expect((await file.body()).subarray(8, 12).toString("ascii")).toBe("WEBP");
  await shot(page, "5-shop-admin-product");
  await signOut(page);

  // The Owner reads the ways and has nothing to save.
  await signIn(page, owner.userName, ownerPassword);
  await page.goto("/shop/contacts");
  await expect(page.getByText("این‌ها را فقط مدیر فروشگاه تغییر می‌دهد.")).toBeVisible();
  await expect(page.getByText("pasargad_gym")).toBeVisible();
  await expect(page.getByRole("button", { name: "ذخیره" })).toHaveCount(0);
  await shot(page, "6-owner-reads");
});

/** The status the page's own host gives a path, asked from inside the page (as siteApp.spec.ts). */
function statusOf(page: Page, path: string): Promise<number> {
  return page.evaluate(`fetch("${path}").then((response) => response.status)`) as Promise<number>;
}

/**
 * The status Caddy and the API give a path sent exactly as written, the way a hand-made request
 * would, past the URL cleaning every browser and Playwright's own request do. Sent to this
 * machine's port 443 with the host's name, since Node may not resolve *.localhost by itself.
 */
function rawStatus(host: string, path: string): Promise<number> {
  return new Promise((resolve, reject) => {
    const request = httpsRequest(
      {
        host: "127.0.0.1",
        port: 443,
        path,
        method: "GET",
        servername: host,
        headers: { Host: host },
        // Caddy signs these names with its own local authority.
        rejectUnauthorized: false,
      },
      (response) => {
        response.resume();
        resolve(response.statusCode ?? 0);
      },
    );
    request.on("error", reject);
    request.end();
  });
}

/**
 * More of a shop than one product, made through the API as the shop admin's screens would: a
 * product out of stock, one with two variants and two photos, one without a photo, and a hidden
 * one. The photos are the public page's own placeholders.
 */
async function fillTheShop(api: APIRequestContext) {
  const login = await api.post("/api/auth/login", {
    data: { userName: shopAdmin.userName, password: shopAdmin.password },
  });
  expect(login.status(), "the shop admin's login").toBe(200);
  const headers = {
    Authorization: `Bearer ${((await login.json()) as { accessToken: string }).accessToken}`,
  };

  const create = async (path: string, data: unknown) => {
    const response = await api.post(path, { headers, data });
    expect(response.status(), `${path}: ${await response.text()}`).toBe(201);
    return ((await response.json()) as { id: string }).id;
  };
  const variant = (label: string, price: number, stock: number) => ({ label, price, stock });
  const product = async (
    name: string,
    categoryId: string,
    variants: ReturnType<typeof variant>[],
    more: { description?: string; isVisible?: boolean; photos?: string[] } = {},
  ) => {
    const id = await create("/api/shop/products", {
      name,
      description: more.description ?? null,
      categoryId,
      isVisible: more.isVisible ?? true,
      variants,
    });
    for (const photo of more.photos ?? []) {
      const uploaded = await api.post(`/api/shop/products/${id}/photos`, {
        headers,
        multipart: {
          file: {
            name: `${photo}.webp`,
            mimeType: "image/webp",
            buffer: readFileSync(`public-site/img/${photo}.webp`),
          },
        },
      });
      expect(uploaded.status(), `photo ${photo}: ${await uploaded.text()}`).toBe(200);
    }
    return id;
  };

  const categories = (await (await api.get("/api/shop/categories", { headers })).json()) as {
    id: string;
    name: string;
  }[];
  const supplements = categories.find((category) => category.name === "مکمل")!.id;
  const wear = await create("/api/shop/categories", { name: "پوشاک" });
  const gear = await create("/api/shop/categories", { name: "لوازم" });
  // Empty: it must not become a chip.
  await create("/api/shop/categories", { name: "عطر" });

  await product("کالای پنهان", supplements, [variant("", 100_000, 1)], { isVisible: false });
  await product("شیکر", gear, [variant("", 180_000, 12)]);
  await product("تی‌شرت ورزشی", wear, [variant("مدیوم", 480_000, 0), variant("لارج", 520_000, 0)], {
    photos: ["shop-wear"],
  });
  // The newest, so the first card of the showcase.
  await product(
    "کراتین مونوهیدرات",
    supplements,
    [variant("۳۰۰ گرم", 890_000, 5), variant("۵۰۰ گرم", 1_350_000, 0)],
    {
      description: "کراتین خالص، بدون طعم.\nروزی یک پیمانهٔ ۵ گرمی، بعد از تمرین.",
      photos: ["shop-creatine", "shop-supplements"],
    },
  );
}

// After the test above, in this order: the shop admin, the contact ways and «پروتئین وی» are its.
test("Showcase_OnTheGymsPublicAddress_ShowsTheShopToAnyone", async ({ page, request }) => {
  await fillTheShop(request);

  // The gym's page: its «فروشگاه» section holds the newest products in place of the shelves.
  await page.goto(publicURL);
  const section = page.locator("#shop");
  const newest = section.getByRole("list", { name: "تازه‌های فروشگاه" });
  await expect(newest.getByRole("listitem")).toHaveCount(4);
  await expect(newest.getByRole("listitem").first()).toContainText("کراتین مونوهیدرات");
  await expect(newest.getByRole("listitem").first()).toContainText("از ۸۹۰٬۰۰۰ تومان");
  await expect(newest.getByRole("link", { name: /تی‌شرت ورزشی/ })).toContainText("ناموجود");
  await expect(section.getByText("کالای پنهان")).toHaveCount(0);
  await expect(section.getByRole("heading", { name: "پروتئین", exact: true })).toBeHidden();
  await section.scrollIntoViewIfNeeded();
  await shotOf(section, "7-public-page-shop-1440");

  // Of the API, this host reaches the showcase and nothing else.
  expect(await statusOf(page, "/api/site/shop/showcase")).toBe(200);
  expect(await statusOf(page, "/api/shop/products")).toBe(404);
  expect(await statusOf(page, "/api/site/me")).toBe(404);
  expect(await statusOf(page, "/api/auth/refresh")).toBe(404);
  // Nor by a path Caddy reads as the shop's and the API would have read as another (16.4's
  // review): an encoded slash is refused before anything routes it, on every host.
  const publicHost = new URL(publicURL).host;
  expect(await rawStatus(publicHost, "/api/site/shop/a%2fb%2fc/../../../auth/login")).toBe(400);
  expect(await rawStatus(publicHost, "/api/site/shop/a%2Fb/%2e%2e/%2e%2e/auth/code")).toBe(400);
  expect(await rawStatus(new URL(siteURL).host, "/api/site/a%2fb/../../members")).toBe(400);
  expect(await rawStatus(publicHost, "/api/site/shop/showcase")).toBe(200);

  // «همهٔ کالاها» opens the shop on the same host; a category's chip filters it.
  await section.getByRole("link", { name: "همهٔ کالاها" }).click();
  await expect(page).toHaveURL(`${publicURL}/shop/`);
  const cards = page.getByRole("list", { name: "کالاهای فروشگاه" });
  await expect(cards.getByRole("listitem")).toHaveCount(4);
  const chips = page.getByRole("navigation", { name: "دسته‌بندی" });
  await expect(chips.getByRole("link")).toHaveText(["همه", "پوشاک", "لوازم", "مکمل"]);
  await shot(page, "8-shop-1440");
  await chips.getByRole("link", { name: "مکمل" }).click();
  await expect(cards.getByRole("listitem")).toHaveCount(2);
  await expect(page.getByRole("region", { name: "راه‌های تماس با فروشگاه" })).toContainText(
    "۰۹۱۲ ۱۲۳ ۴۵۶۷",
  );

  // A product's page: its variants, and its photo from the files volume through this host.
  await cards.getByRole("link", { name: /کراتین مونوهیدرات/ }).click();
  await expect(page).toHaveURL(new RegExp(`^${publicURL}/shop/product/[0-9a-f-]{36}$`));
  await expect(page.getByRole("heading", { level: 1, name: "کراتین مونوهیدرات" })).toBeVisible();
  const variants = page.getByRole("list", { name: "مدل‌ها و قیمت" }).getByRole("listitem");
  await expect(variants.nth(0)).toContainText("۸۹۰٬۰۰۰ تومان");
  await expect(variants.nth(1)).toContainText("ناموجود");
  const photo = page.getByRole("img", { name: "کراتین مونوهیدرات" });
  await expect(photo).toBeVisible();
  // Loaded, not a broken image: a photo that failed has no width of its own.
  await expect(photo).not.toHaveJSProperty("naturalWidth", 0);
  // Kept by the browser for a year, through this host too: the page's own "no-cache" is set for
  // its files only, never added to the API's answer.
  // Asked from inside the page, so from its own host (Node does not resolve *.localhost).
  const photoCaching = await page.evaluate(
    `fetch(${JSON.stringify(await photo.getAttribute("src"))}, { cache: "no-store" })
       .then((response) => [response.status, response.headers.get("cache-control")])`,
  );
  expect(photoCaching).toEqual([200, "public, max-age=31536000, immutable"]);
  await page.getByRole("button", { name: "عکس ۲" }).click();
  await shot(page, "9-product-1440");

  // The product's own address, opened afresh (a link someone was sent): Caddy answers every
  // address under /shop/ with the shop's page.
  const address = page.url();
  const reopened = (await page.goto(address))!;
  expect(reopened.status()).toBe(200);
  await expect(page.getByRole("heading", { level: 1, name: "کراتین مونوهیدرات" })).toBeVisible();

  // /shop without its slash, and the same pages asked of the app's host, both end here.
  await page.goto(`${publicURL}/shop`);
  await expect(page).toHaveURL(`${publicURL}/shop/`);
  await page.goto(`${siteURL}/shop/`);
  await expect(page).toHaveURL(`${publicURL}/shop/`);
  await expect(cards.getByRole("listitem")).toHaveCount(4);

  // On a phone: two cards a row and nothing wider than the screen, on all three screens.
  await page.setViewportSize({ width: 390, height: 844 });
  const noSidewaysScroll = () =>
    page.evaluate("document.documentElement.scrollWidth <= window.innerWidth") as Promise<boolean>;
  expect(await noSidewaysScroll()).toBe(true);
  await shot(page, "10-shop-390");
  await page.goto(address);
  await expect(page.getByRole("heading", { level: 1, name: "کراتین مونوهیدرات" })).toBeVisible();
  expect(await noSidewaysScroll()).toBe(true);
  await shot(page, "11-product-390");
  await page.goto(publicURL);
  await expect(newest.getByRole("listitem")).toHaveCount(4);
  await section.scrollIntoViewIfNeeded();
  expect(await noSidewaysScroll()).toBe(true);
  await shotOf(section, "12-public-page-shop-390");
});

test("Showcase_TheApiDoesNotAnswer_ThePublicPageKeepsItsShelves", async ({ page }) => {
  // The page never depends on the API (§16 *The showcase*): with no answer, the six shelves stay.
  await page.route("**/api/site/shop/showcase", (route) => route.abort());

  await page.goto(publicURL);

  const section = page.locator("#shop");
  await section.scrollIntoViewIfNeeded();
  await expect(section.getByRole("heading", { name: "پروتئین", exact: true })).toBeVisible();
  await expect(section.locator(".shelf")).toHaveCount(6);
  await expect(section.getByRole("list", { name: "تازه‌های فروشگاه" })).toBeHidden();
  await expect(section.getByRole("link", { name: "همهٔ کالاها" })).toBeHidden();
});
