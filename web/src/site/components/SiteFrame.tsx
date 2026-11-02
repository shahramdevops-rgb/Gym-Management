import type { ReactNode } from "react";

/**
 * The site app's page: the gym's logo and name on top, then the screen, in one narrow column
 * made for a phone held in one hand. The logo is the app's own icon, dark stone on bronze, so the
 * first screen looks like what the member tapped (asked by the developer, 1405/08/08); the soft
 * glow is the panel's sign-in look (13.2g).
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
          <img
            src="/icon-192.png"
            alt=""
            aria-hidden
            className="size-16 rounded-2xl shadow-lg shadow-black/25"
          />
          <p className="text-lg font-extrabold">باشگاه پاسارگاد</p>
        </div>

        {children}
      </div>
    </div>
  );
}
