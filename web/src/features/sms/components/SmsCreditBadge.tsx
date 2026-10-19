import { MessageSquareText } from "lucide-react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { formatMoney } from "@/lib/format";
import { cn } from "@/lib/utils";

import {
  smsCreditEmptyToman,
  smsCreditFairToman,
  smsCreditLowToman,
  smsCreditRefreshMs,
  useSmsCredit,
} from "../api";

type Tone = "empty" | "low" | "fair" | "good" | "stale";

/**
 * The whole pill flashing below 1,000 Toman, red below 100,000, orange up to 200,000 (both
 * included), green above.
 */
function toneOf(toman: number): Tone {
  if (toman < smsCreditEmptyToman) {
    return "empty";
  }
  if (toman < smsCreditLowToman) {
    return "low";
  }
  return toman <= smsCreditFairToman ? "fair" : "good";
}

const toneClasses: Record<Tone, { pill: string; icon: string }> = {
  // The icon takes the text colour, so it stays visible on the solid red of the flash.
  empty: {
    pill: "border-destructive bg-destructive/10 text-destructive animate-credit-blink motion-reduce:animate-none",
    icon: "",
  },
  low: {
    pill: "border-destructive/40 bg-destructive/10 hover:bg-destructive/15",
    icon: "text-destructive",
  },
  fair: { pill: "border-warning/40 bg-warning/10 hover:bg-warning/15", icon: "text-warning" },
  good: { pill: "border-success/40 bg-success/10 hover:bg-success/15", icon: "text-success" },
  stale: { pill: "border-border bg-muted text-muted-foreground hover:bg-muted/80", icon: "" },
};

/**
 * The Owner's SMS credit in the header, on every page (BUSINESS_RULES.md §10 *The credit in the
 * header*): its colour says how much is left, and it opens «تنظیمات پیامک». Asked again every 10
 * minutes; when Kavenegar does not answer, the last figure it gave stays, in grey. Nothing in test
 * mode, where nothing is really sent.
 */
export function SmsCreditBadge() {
  const credit = useSmsCredit({ refetchInterval: smsCreditRefreshMs });

  if (credit.data?.isTestMode === true || (credit.data === undefined && !credit.isError)) {
    return null;
  }

  // A failed request keeps the previous answer in `data`, so both kinds of silence end up here.
  const lastKnown = credit.data?.lastKnownToman ?? null;
  const answered = !credit.isError && credit.data?.remainingToman != null;
  const tone: Tone = answered && lastKnown !== null ? toneOf(Number(lastKnown)) : "stale";

  return (
    <Link
      to={paths.smsSettings}
      title={tone === "stale" ? "کاوه‌نگار الان جواب نمی‌دهد" : undefined}
      className={cn(
        "inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3 text-sm",
        "shadow-xs transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
        toneClasses[tone].pill,
      )}
    >
      <span className="relative flex size-4 shrink-0 items-center justify-center">
        {tone === "low" && (
          <span
            aria-hidden
            className="absolute inline-flex size-full rounded-full bg-destructive/40 motion-safe:animate-ping"
          />
        )}
        <MessageSquareText aria-hidden className={cn("relative size-4", toneClasses[tone].icon)} />
      </span>
      <span className="sr-only sm:not-sr-only">اعتبار پیامک:</span>
      <span className="font-medium whitespace-nowrap">
        {lastKnown === null ? "نامعلوم" : formatMoney(lastKnown)}
      </span>
      {tone === "empty" && <span className="sr-only">(تقریباً تمام شده)</span>}
      {tone === "low" && <span className="sr-only">(کم است)</span>}
      {tone === "stale" && lastKnown !== null && (
        <span className="sr-only">(آخرین عدد؛ کاوه‌نگار الان جواب نمی‌دهد)</span>
      )}
    </Link>
  );
}
