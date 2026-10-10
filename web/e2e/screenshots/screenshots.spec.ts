import { mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { expect, test, type Page } from "@playwright/test";

import { changeOwnerPassword, ownerPassword } from "../seed";
import { owner } from "../stack";

/**
 * The README's screenshots, in the light and the dark theme (roadmap Phase 12), taken as the Owner
 * from the made-up gym of `admin seed-demo`. Each run overwrites docs/screenshots/.
 */

const outputDir = fileURLToPath(new URL("../../../docs/screenshots/", import.meta.url));

interface Shot {
  name: string;
  /** Null for the profile of the member at the top of the list: the one who came in last. */
  path: string | null;
  /** Text the page is scrolled to before the picture, for what sits below the first screen. */
  scrollTo?: string;
}

const shots: Shot[] = [
  { name: "desk", path: "/" },
  { name: "inside", path: "/attendance" },
  { name: "members", path: "/members" },
  { name: "member", path: null },
  { name: "dashboard", path: "/dashboard" },
  { name: "dashboard-charts", path: "/dashboard", scrollTo: "درآمد و هزینه" },
  { name: "history", path: "/history" },
  { name: "cafe", path: "/cafe/orders" },
  { name: "payables", path: "/payables" },
];

const themes = ["light", "dark"] as const;

test.beforeAll(async ({ request }) => {
  await changeOwnerPassword(request);
  mkdirSync(outputDir, { recursive: true });
});

for (const theme of themes) {
  test(`Screenshots_${theme}Theme_EveryMainScreen`, async ({ page }) => {
    // The key lib/theme.ts reads, set before the page draws so it opens in this theme.
    await page.addInitScript((value) => localStorage.setItem("gym.theme", value), theme);
    await logIn(page);

    for (const shot of shots) {
      if (shot.path === null) {
        await page.goto("/members");
        await settle(page);
        await page.locator('main a[href^="/members/"]:not([href$="/new"])').first().click();
      } else {
        await page.goto(shot.path);
      }

      await settle(page);
      if (shot.scrollTo) {
        // Wheel the page until the text sits just under the header (about 60 px high).
        const box = await page.getByText(shot.scrollTo, { exact: true }).first().boundingBox();
        await page.mouse.move(600, 500);
        await page.mouse.wheel(0, (box?.y ?? 0) - 90);
        await page.waitForTimeout(500);
      }

      await page.screenshot({
        path: `${outputDir}${shot.name}-${theme}.png`,
        animations: "disabled",
      });
    }
  });
}

async function logIn(page: Page) {
  await page.goto("/");
  await page.getByLabel("نام کاربری").fill(owner.userName);
  await page.getByLabel("رمز عبور", { exact: true }).fill(ownerPassword);
  await page.getByRole("button", { name: "ورود" }).click();
  await expect(page.getByRole("heading", { name: "ورود با کمد" })).toBeVisible();
}

/** The page's heading, its data, its font, and the charts' entrance animation, before the picture. */
async function settle(page: Page) {
  await expect(page.getByRole("heading", { level: 2 }).first()).toBeVisible();
  await page.waitForLoadState("networkidle");
  // A string, because this file is compiled for Node, which has no `document`.
  await page.waitForFunction("document.fonts.status === 'loaded'");
  await page.waitForTimeout(1500);
}
