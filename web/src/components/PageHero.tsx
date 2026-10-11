import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import logo from "@/assets/brand/pasargad-logo.png";
import { cn } from "@/lib/utils";

import { usePageSection } from "./pageSection";

interface PageHeroProps {
  title: ReactNode;
  /** The page's own tools (a search, a switch), at the end of the title row, on the banner. */
  actions?: ReactNode;
  /** Under the title row, still on the banner: a strip of the page's figures, on a card. */
  children?: ReactNode;
  /** The icon before the title. Without one, the page's menu item's icon, given by the frame. */
  icon?: LucideIcon;
  className?: string;
}

/**
 * The top of a page as a banner in its menu group's colour, the shape of the dashboard's (13.2h,
 * asked by the developer, 1405/07/19): the page's icon and title in white, its tools at the end of
 * the same row, its figures on a card under them, and the gym's logo at the far end. Kept slim
 * (asked by the developer): the desk works under it all day. The colour is `--tone`, set on the
 * page by the frame, the same colour as the group's dot and line in the menu, darkened by
 * `hero-tone` so white text reads on it in both themes. The logo and the soft light behind it are decoration.
 */
export function PageHero({ title, actions, children, icon, className }: PageHeroProps) {
  const section = usePageSection();
  const Icon = icon ?? section?.icon;

  return (
    <div
      className={cn(
        "relative isolate overflow-hidden rounded-3xl hero-tone px-5 py-3.5 text-white shadow-raised",
        className,
      )}
    >
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
        <div className="absolute -bottom-28 start-1/3 size-64 rounded-full bg-white/10 blur-3xl" />
      </div>

      <div className="flex items-center gap-6">
        <div className="min-w-0 flex-1 space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3">
            <div className="flex min-w-0 items-center gap-3">
              {Icon !== undefined && (
                <span
                  aria-hidden
                  className="grid size-10 shrink-0 place-items-center rounded-xl bg-white/15 ring-1 ring-white/25"
                >
                  <Icon className="size-5.5" />
                </span>
              )}
              <h2 className="text-2xl leading-tight font-extrabold tracking-tight">{title}</h2>
            </div>
            {actions !== undefined && (
              <div className="flex flex-wrap items-center gap-2">{actions}</div>
            )}
          </div>
          {children}
        </div>

        <div className="relative -my-2 hidden shrink-0 lg:block">
          <div
            aria-hidden
            className="absolute inset-[-15%] -z-10 rounded-full bg-white/15 blur-2xl"
          />
          <img
            src={logo}
            alt=""
            className="h-24 w-28 object-fill drop-shadow-[0_6px_14px_rgb(0_0_0/0.3)]"
          />
        </div>
      </div>
    </div>
  );
}
