import type { LucideIcon } from "lucide-react";

import { StatTile } from "@/components/StatTile";
import { formatMoney } from "@/lib/format";

export interface HistoryTotal {
  label: string;
  amount: number | string;
  icon: LucideIcon;
}

/**
 * The totals row under a section's table (BUSINESS_RULES.md §12 Totals in the history): what
 * every row the filters let through comes to, all pages together, not only the one on screen.
 * Each total is a tile in the page's colour (roadmap 13.2e).
 */
export function HistoryTotals({ totals }: { totals: HistoryTotal[] }) {
  return (
    <section aria-label="جمع" className="space-y-2">
      <p className="text-sm font-medium">جمع همهٔ ردیف‌ها</p>
      <dl className="grid gap-3 sm:grid-cols-3">
        {totals.map((total) => (
          <StatTile
            key={total.label}
            label={total.label}
            value={formatMoney(total.amount)}
            icon={total.icon}
          />
        ))}
      </dl>
    </section>
  );
}
