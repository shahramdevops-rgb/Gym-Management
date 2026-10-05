import type { ReactNode } from "react";

import logo from "@/assets/brand/pasargad-logo.png";
import { formatDate } from "@/lib/format";

import type { RangeDraft } from "./RangePicker";

interface DashboardHeroProps {
  range: RangeDraft;
  /** The range picker, on the banner's colour. */
  children: ReactNode;
}

/**
 * The top of the dashboard: the gym's logo and the page's title on a green banner, the range the
 * figures below are for, and the range picker (the logo and the green asked by the developer,
 * 1405/07/14).
 *
 * The logo is white lines on transparency, cut from a photo of the gym's wall, so it sits on the
 * banner's colour in both themes. It stands at the banner's far end, the left, large and on a soft
 * light of its own, with the title and the range at the start (asked by the developer, 1405/07/14).
 * The soft shapes behind are decoration, hidden from screen readers.
 */
export function DashboardHero({ range, children }: DashboardHeroProps) {
  const complete = range.from !== undefined && range.to !== undefined;

  return (
    <div className="relative isolate overflow-hidden rounded-3xl bg-linear-to-l from-(--hero-from) via-(--hero-via) to-(--hero-to) p-6 text-white shadow-lg">
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
        <div className="absolute -bottom-28 start-1/3 size-64 rounded-full bg-white/10 blur-3xl" />
      </div>

      <div className="flex items-center justify-between gap-6">
        <div className="min-w-0 space-y-5">
          <div className="space-y-1">
            <h2 className="text-3xl font-extrabold tracking-tight">داشبورد</h2>
            {complete && (
              <p className="text-sm text-white/85">
                گزارش از {formatDate(range.from)} تا {formatDate(range.to)}
              </p>
            )}
          </div>
          {children}
        </div>

        <div className="relative -my-3 hidden shrink-0 sm:block">
          <div
            aria-hidden
            className="absolute inset-[-15%] -z-10 rounded-full bg-white/15 blur-2xl"
          />
          <img
            src={logo}
            alt="پاسارگاد"
            className="h-36 w-32 object-fill drop-shadow-[0_6px_14px_rgb(0_0_0/0.3)] md:h-44 md:w-40"
          />
        </div>
      </div>
    </div>
  );
}
