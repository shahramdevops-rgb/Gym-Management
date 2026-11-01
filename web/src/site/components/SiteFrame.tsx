import type { ReactNode } from "react";

/**
 * The site app's page: the screen in one narrow column made for a phone held in one hand. No logo
 * or gym name on top (the developer, 1405/08/09: it took a third of the first screen). The soft
 * glow is the panel's sign-in look (13.2g), so the two read as the same gym.
 */
export function SiteFrame({ children }: { children: ReactNode }) {
  return (
    <div className="isolate flex min-h-dvh justify-center overflow-hidden bg-background px-4 py-8">
      <div className="relative w-full max-w-sm space-y-6">
        <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
          <div className="absolute -start-24 top-0 size-80 rounded-full bg-primary/20 blur-3xl" />
          <div className="absolute -end-24 bottom-0 size-80 rounded-full bg-(--tone-violet)/20 blur-3xl" />
        </div>

        {children}
      </div>
    </div>
  );
}
