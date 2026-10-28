import type { ReactNode } from "react";

import logo from "@/assets/brand/pasargad-logo.png";

/**
 * The site app's page: the gym's logo and name on top, then the screen, in one narrow column
 * made for a phone held in one hand. The logo square and the soft glow are the panel's sign-in
 * look (13.2g), so the two read as the same gym.
 */
export function SiteFrame({ children }: { children: ReactNode }) {
  return (
    <div className="isolate flex min-h-dvh justify-center overflow-hidden bg-background px-4 py-8">
      <div className="relative w-full max-w-sm space-y-6">
        <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
          <div className="absolute -start-24 top-0 size-80 rounded-full bg-primary/20 blur-3xl" />
          <div className="absolute -end-24 bottom-0 size-80 rounded-full bg-(--tone-violet)/20 blur-3xl" />
        </div>

        <div className="flex flex-col items-center gap-3 text-center">
          <span
            aria-hidden
            className="flex size-16 items-center justify-center rounded-2xl bg-linear-to-br from-primary to-(--tone-violet) p-2 shadow-lg shadow-primary/30"
          >
            <img src={logo} alt="" className="size-full object-contain" />
          </span>
          <p className="text-lg font-extrabold">باشگاه پاسارگاد</p>
        </div>

        {children}
      </div>
    </div>
  );
}
