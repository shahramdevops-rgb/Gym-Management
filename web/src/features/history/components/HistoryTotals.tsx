import { formatMoney } from "@/lib/format";

export interface HistoryTotal {
  label: string;
  amount: number | string;
}

/**
 * The totals row under a section's table (BUSINESS_RULES.md §12 Totals in the history): what
 * every row the filters let through comes to, all pages together, not only the one on screen.
 */
export function HistoryTotals({ totals }: { totals: HistoryTotal[] }) {
  return (
    <section aria-label="جمع" className="space-y-2 rounded-md border bg-muted/40 p-3">
      <p className="text-sm font-medium">جمع همهٔ ردیف‌ها</p>
      <dl className="grid gap-3 sm:grid-cols-3">
        {totals.map((total) => (
          <div key={total.label} className="space-y-1">
            <dt className="text-sm text-muted-foreground">{total.label}</dt>
            <dd className="font-semibold">{formatMoney(total.amount)}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
