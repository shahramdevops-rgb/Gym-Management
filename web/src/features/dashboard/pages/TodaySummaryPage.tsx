import { DoorOpen, RefreshCw, Wallet, type LucideIcon } from "lucide-react";

import { PageHero } from "@/components/PageHero";
import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import { formatLongDate, formatMoney, formatNumber, formatTime } from "@/lib/format";
import { toneStyle, type Tone } from "@/lib/tone";
import { cn } from "@/lib/utils";

import { useTodaySummary, type TodaySummary } from "../api";

/**
 * «خلاصهٔ امروز»: today at a glance on the Owner's phone (BUSINESS_RULES.md §12 *Today's summary*,
 * roadmap 13.5), instead of a home-screen widget, which a web app cannot have. Who is inside now,
 * today's check-ins, today's «دریافتی» and the plans and single visits sold, the daily report's
 * figures for today so far. Owner only; on a computer it is the same page, a little wider.
 *
 * The figures are those of the moment written at the top: asked when the page opens, when the phone
 * comes back to it, and on «به‌روزرسانی», never on a timer (the developer, 1405/08/03).
 */
export function TodaySummaryPage() {
  const summary = useTodaySummary();
  const updatedAt = new Date(summary.dataUpdatedAt);

  return (
    <div className="mx-auto max-w-xl space-y-4">
      <PageHero
        title="خلاصهٔ امروز"
        actions={
          <Button
            variant="outline"
            size="sm"
            onClick={() => void summary.refetch()}
            disabled={summary.isFetching}
          >
            <RefreshCw
              aria-hidden
              className={cn(summary.isFetching && "motion-safe:animate-spin")}
            />
            به‌روزرسانی
          </Button>
        }
      >
        {/* Kept when a refresh fails too: the date then says whether the figures are yesterday's. */}
        {summary.data !== undefined && (
          <p className="text-sm text-muted-foreground">
            {formatLongDate(updatedAt)} · به‌روز در {formatTime(updatedAt.toISOString())}
          </p>
        )}
      </PageHero>

      {summary.isPending && <LoadingState />}

      {summary.isError && (
        <Alert variant="destructive">
          {summary.data === undefined
            ? errorMessage(summary.error)
            : // A failed refresh keeps the last figures on screen, and says how old they are.
              `${errorMessage(summary.error)} عددهای زیر مال ساعت ${formatTime(updatedAt.toISOString())} هستند.`}
        </Alert>
      )}

      {summary.data !== undefined && <Figures summary={summary.data} />}

      <p className="text-center text-xs text-muted-foreground">
        با باز کردن این صفحه، برگشتن به برنامه یا دکمهٔ «به‌روزرسانی» تازه می‌شود.
      </p>
    </div>
  );
}

function Figures({ summary }: { summary: TodaySummary }) {
  const guests = Number(summary.guestsInside);

  return (
    <dl className="space-y-3">
      <SummaryCard
        label="داخل باشگاه، همین الان"
        tone="sky"
        icon={DoorOpen}
        value={`${formatNumber(Number(summary.insideNow))} نفر`}
        hint={guests > 0 ? `${formatNumber(guests)} نفرشان مهمان‌اند` : undefined}
        large
      />
      <SummaryCard
        label="دریافتی امروز"
        tone="violet"
        icon={Wallet}
        value={formatMoney(summary.received)}
        hint="بدهی‌های قبلی هم، منهای بازگشت‌ها؛ بدون آنالیز، فروشگاه و متفرقه"
      />
      {/* Three short figures side by side: a 390px phone has room for each one's word and number. */}
      <div className="grid grid-cols-3 gap-3">
        <SummaryCard
          label="ورود امروز"
          tone="blue"
          value={formatNumber(Number(summary.checkIns))}
          hint="بدون مهمان"
        />
        <SummaryCard
          label="پلن"
          tone="orange"
          value={formatNumber(Number(summary.plansSold))}
          hint="فروخته‌شده"
        />
        <SummaryCard
          label="تک‌جلسه"
          tone="teal"
          value={formatNumber(Number(summary.singleVisitsSold))}
          hint="فروخته‌شده"
        />
      </div>
    </dl>
  );
}

interface SummaryCardProps {
  label: string;
  value: string;
  tone: Tone;
  /** Only the two wide cards have one; the three small ones keep their room for the figure. */
  icon?: LucideIcon;
  hint?: string;
  /** The figure the Owner opens the page for, set larger. */
  large?: boolean;
}

/**
 * One figure, edged like the dashboard's `StatCard` (a faint frame and a thick start edge in its
 * tone) but without a comparison: today so far has no fair "before" to compare with. The label and
 * the figure are the card's own `dt` and `dd`, so the cards stay one list.
 */
function SummaryCard({ label, value, tone, icon: Icon, hint, large = false }: SummaryCardProps) {
  return (
    <div
      style={toneStyle(tone)}
      className="relative space-y-1 rounded-xl border border-s-[3px] tone-edge bg-card p-4 max-lg:p-3"
    >
      <dt className={cn("text-sm text-muted-foreground", Icon !== undefined && "pe-12")}>
        {label}
        {Icon !== undefined && (
          <span
            aria-hidden
            className="absolute end-4 top-4 grid size-10 place-items-center rounded-xl tone-soft tone-ink max-lg:end-3 max-lg:top-3"
          >
            <Icon className="size-5" />
          </span>
        )}
      </dt>
      <dd
        className={cn(
          "font-extrabold tracking-tight whitespace-nowrap",
          large ? "text-4xl" : "text-2xl",
        )}
      >
        {value}
      </dd>
      {hint !== undefined && <dd className="text-xs text-muted-foreground">{hint}</dd>}
    </div>
  );
}
