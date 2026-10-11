import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

interface StatTileProps {
  label: string;
  value: ReactNode;
  icon: LucideIcon;
  /** The value's accessible name, when the label alone would not say which total it is. */
  valueLabel?: string;
  /** The tile that matters most in its row: its value set larger. */
  primary?: boolean;
  className?: string;
}

/**
 * One total on a working page (roadmap 13.2e), in the dashboard's stat-card shape: the label, the
 * amount, and an icon in a square of the page's colour (`--tone`, grey without one). Simpler than
 * the dashboard's `StatCard`: no comparison, no progress. A `dt` and a `dd`, so tiles go inside a
 * `<dl>`.
 */
export function StatTile({
  label,
  value,
  icon: Icon,
  valueLabel,
  primary,
  className,
}: StatTileProps) {
  return (
    <div
      className={cn(
        // Edged like the page's banner (13.2h): a faint frame and a thick start edge in its tone.
        "flex items-center gap-3 rounded-xl border border-s-[3px] tone-edge bg-card p-3",
        className,
      )}
    >
      <span
        aria-hidden
        className="grid size-10 shrink-0 place-items-center rounded-xl tone-soft tone-ink"
      >
        <Icon className="size-5" />
      </span>
      <div className="min-w-0 space-y-0.5">
        <dt className="text-sm text-muted-foreground">{label}</dt>
        <dd
          aria-label={valueLabel}
          className={cn(
            "font-bold tracking-tight whitespace-nowrap",
            primary ? "text-xl font-extrabold" : "text-lg",
          )}
        >
          {value}
        </dd>
      </div>
    </div>
  );
}
