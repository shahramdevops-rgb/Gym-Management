import { HandCoins } from "lucide-react";

import { formatMoney } from "@/lib/format";

import type { Receivables } from "../api";
import { toneStyle, type Tone } from "../tone";

interface Bucket {
  label: string;
  amount: number | string;
  tone: Tone;
}

/**
 * What everyone owes today (§12 *Receivables*): the total, and how old the debts are in three
 * buckets. A bar split by the buckets' shares shows at a glance how much is getting old, from
 * green to red; the amounts are written beside it, so the bar is only a picture of them.
 */
export function ReceivablesCard({ data }: { data: Receivables }) {
  const buckets: Bucket[] = [
    { label: "مطالبات ۰ تا ۷ روزه", amount: data.upTo7Days, tone: "green" },
    { label: "مطالبات ۸ تا ۳۰ روزه", amount: data.from8To30Days, tone: "amber" },
    { label: "مطالبات بیش از ۳۰ روز", amount: data.over30Days, tone: "red" },
  ];
  const total = buckets.reduce((sum, bucket) => sum + Math.max(0, Number(bucket.amount)), 0);

  return (
    <div
      style={toneStyle("violet")}
      className="relative space-y-4 overflow-hidden rounded-2xl border bg-card p-4 shadow-sm before:absolute before:inset-x-0 before:top-0 before:h-1 before:bg-(--tone)"
    >
      <dl>
        <div className="space-y-1.5">
          <dt className="pe-12 text-sm text-muted-foreground">
            کل مطالبات
            <span
              aria-hidden
              className="absolute end-4 top-4 grid size-10 place-items-center rounded-xl tone-soft tone-ink"
            >
              <HandCoins className="size-5" />
            </span>
          </dt>
          <dd className="pe-12 text-2xl font-extrabold tracking-tight">
            {formatMoney(data.total)}
          </dd>
          <dd className="text-xs text-muted-foreground">آنچه همه تا امروز بدهکارند</dd>
        </div>
      </dl>

      <div aria-hidden className="flex h-2.5 gap-0.5 overflow-hidden rounded-full bg-muted">
        {total > 0 &&
          buckets.map((bucket) => {
            const share = (Math.max(0, Number(bucket.amount)) / total) * 100;
            return share > 0 ? (
              <div
                key={bucket.label}
                className="h-full tone-solid"
                style={{ ...toneStyle(bucket.tone), inlineSize: `${share}%` }}
              />
            ) : null;
          })}
      </div>

      <dl className="grid gap-2 sm:grid-cols-3">
        {buckets.map((bucket) => (
          <div
            key={bucket.label}
            style={toneStyle(bucket.tone)}
            className="space-y-0.5 rounded-xl tone-soft px-3 py-2"
          >
            <dt className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <span aria-hidden className="size-2 shrink-0 rounded-full tone-solid" />
              {bucket.label}
            </dt>
            <dd className="text-sm font-bold">{formatMoney(bucket.amount)}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
