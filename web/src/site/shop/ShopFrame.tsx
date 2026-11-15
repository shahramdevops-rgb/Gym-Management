import type { ReactNode } from "react";
import { Link } from "react-router";

// The public page's own bronze logo, from its folder: one file for the page and the shop.
import logo from "../../../public-site/img/favicon.png";

import { gymPageAddress, shopPaths } from "./paths";

/**
 * The shop's page (roadmap 16.4): the gym's name on top, as the public page has it, leading back
 * to that page; the screen under it, wide on a computer and one column on a phone. The public
 * page's top bar is not copied whole: its links are that page's sections.
 */
export function ShopFrame({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col bg-background text-foreground">
      <header className="sticky top-0 z-20 border-b bg-background/85 backdrop-blur-md">
        <div className="mx-auto flex min-h-16 w-full max-w-6xl items-center justify-between gap-4 px-5">
          <a
            href={gymPageAddress}
            className="flex items-center gap-3"
            aria-label="پاسارگاد پلاس، صفحه‌ی اول"
          >
            <img src={logo} alt="" className="h-10 w-auto" />
            <span className="text-[1.05rem] font-extrabold">
              پاسارگاد<span className="ms-[0.1em] text-primary">+</span>
            </span>
          </a>

          <nav aria-label="فروشگاه" className="flex items-center gap-1 text-sm">
            <Link
              to={shopPaths.showcase}
              className="px-3 py-2 text-muted-foreground transition-colors hover:text-foreground"
            >
              فروشگاه
            </Link>
            <a
              href={gymPageAddress}
              className="px-3 py-2 text-muted-foreground transition-colors hover:text-foreground"
            >
              سایت باشگاه
            </a>
          </nav>
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 space-y-10 px-5 py-8 sm:py-12">
        {children}
      </main>
    </div>
  );
}
