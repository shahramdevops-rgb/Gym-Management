import { Dumbbell } from "lucide-react";
import type { ReactNode } from "react";

import { useCurrentUser } from "@/features/auth/api";
import { formatDate, formatLongDate } from "@/lib/format";

import type { RangeDraft } from "./RangePicker";

interface DashboardHeroProps {
  range: RangeDraft;
  /** The range picker, on the banner's colour. */
  children: ReactNode;
}

/**
 * The top of the dashboard: the page's title on a coloured banner, a greeting, today's date in
 * words, the range the figures below are for, and the range picker. The shapes behind it are
 * decoration and hidden from screen readers.
 */
export function DashboardHero({ range, children }: DashboardHeroProps) {
  const user = useCurrentUser();
  const complete = range.from !== undefined && range.to !== undefined;

  return (
    <div className="relative isolate overflow-hidden rounded-3xl bg-linear-to-l from-(--hero-from) via-(--hero-via) to-(--hero-to) p-6 text-white shadow-lg">
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
        <div className="absolute -top-24 -end-16 size-72 rounded-full bg-white/10 blur-2xl" />
        <div className="absolute -bottom-28 start-1/3 size-64 rounded-full bg-white/10 blur-3xl" />
        <Dumbbell className="absolute end-6 top-1/2 size-40 -translate-y-1/2 -rotate-12 text-white/10" />
      </div>

      <div className="space-y-5">
        <div className="space-y-1">
          <p className="text-sm text-white/80">
            {user.data !== undefined ? `سلام ${user.data.fullName}، ` : ""}
            {formatLongDate(new Date())}
          </p>
          <h2 className="text-3xl font-extrabold tracking-tight">داشبورد</h2>
          {complete && (
            <p className="text-sm text-white/80">
              گزارش از {formatDate(range.from)} تا {formatDate(range.to)}
            </p>
          )}
        </div>
        {children}
      </div>
    </div>
  );
}
