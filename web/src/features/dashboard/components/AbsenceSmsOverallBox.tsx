import { MessageSquareText } from "lucide-react";

import { formatDate, formatMoney, formatNumber, formatPercent } from "@/lib/format";

import { absenceSmsWindowDays, notMeasurableRule } from "../absenceSmsText";
import type { AbsenceSmsEffect } from "../api";
import { absenceSmsByMonth, absenceSmsRate } from "../figures";
import { AbsenceSmsNamesDialog } from "./AbsenceSmsNamesDialog";
import { ListFrame } from "./ListFrame";

/** «همان روز», «۱ روز بعد», …: the columns of how soon they came. */
function dayLabel(day: number): string {
  return day === 0 ? "همان روز" : `${formatNumber(day)} روز بعد`;
}

/**
 * «اثر پیامک غیبت» in «نیاز به اقدام» (BUSINESS_RULES.md §12 *The absence SMS's effect*, decided
 * with the developer, 1405/08/04): every absence SMS since the first, whatever the dashboard's
 * range, so a new month never empties it. The rate and its counts, how soon those who came back
 * came (most in the first day or two points to the message), the cost, and the same month by
 * month. «فهرست نام‌ها» opens the last 30 days' names.
 */
export function AbsenceSmsOverallBox({ effect }: { effect: AbsenceSmsEffect }) {
  const sent = Number(effect.sent);
  const cameBack = Number(effect.cameBack);
  const didNotComeBack = Number(effect.didNotComeBack);
  const notMeasurable = Number(effect.notMeasurable);
  const rate = absenceSmsRate(cameBack, didNotComeBack);
  const byDay = effect.cameBackByDay.map(Number);
  const busiestDay = Math.max(1, ...byDay);
  const months = absenceSmsByMonth(effect.messages);

  return (
    <ListFrame
      headingId="attention-absence-sms"
      title="اثر پیامک غیبت"
      icon={MessageSquareText}
      tone="teal"
      count={`${formatNumber(sent)} پیامک`}
      rule={`همهٔ پیامک‌های غیبت از ${formatDate(effect.from)} تا امروز. عضوی که تا ${absenceSmsWindowDays} روز بعد از پیامک آمد، «برگشت».`}
    >
      {sent === 0 ? (
        <p className="py-2 text-sm text-muted-foreground">هنوز پیامک غیبتی فرستاده نشده است.</p>
      ) : (
        <div className="space-y-3">
          <p className="flex flex-wrap items-baseline gap-x-2">
            <span className="text-2xl font-extrabold tone-ink">{formatPercent(rate)}</span>
            <span className="text-sm text-muted-foreground">
              {formatNumber(cameBack)} از {formatNumber(cameBack + didNotComeBack)} نفر تا{" "}
              {absenceSmsWindowDays} روز بعد از پیامک آمدند
            </span>
          </p>

          <dl className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-4">
            <Figure label="برنگشتند" value={`${formatNumber(didNotComeBack)} نفر`} />
            <Figure label="در انتظار" value={`${formatNumber(Number(effect.waiting))} نفر`} />
            <Figure label="قابل سنجش نیست" value={`${formatNumber(notMeasurable)} پیامک`} />
            <Figure
              label="میانگین برگشت"
              value={
                effect.averageDaysToComeBack === null
                  ? "—"
                  : `${formatNumber(Number(effect.averageDaysToComeBack))} روز`
              }
            />
            <Figure label="هزینهٔ کل" value={formatMoney(effect.costToman)} />
            <Figure
              label="هزینه برای هر برگشت"
              value={
                effect.costPerReturnToman === null ? "—" : formatMoney(effect.costPerReturnToman)
              }
            />
          </dl>

          <section aria-labelledby="absence-sms-by-day" className="space-y-1.5">
            <h5 id="absence-sms-by-day" className="text-xs font-semibold">
              برگشتی‌ها چند روز بعد از پیامک آمدند
            </h5>
            <ul className="grid grid-cols-6 gap-1.5">
              {byDay.map((count, day) => (
                <li key={day} className="space-y-1 text-center text-xs">
                  <div aria-hidden className="flex h-14 items-end rounded-md bg-muted/60">
                    <div
                      className="w-full rounded-md tone-bar"
                      style={{ blockSize: `${(count / busiestDay) * 100}%` }}
                    />
                  </div>
                  <p className="font-semibold tabular-nums">{formatNumber(count)}</p>
                  <p className="text-muted-foreground">{dayLabel(day)}</p>
                </li>
              ))}
            </ul>
          </section>

          <section aria-labelledby="absence-sms-by-month" className="space-y-1.5">
            <h5 id="absence-sms-by-month" className="text-xs font-semibold">
              ماه به ماه
            </h5>
            <ul className="divide-y text-xs">
              {months.map((month) => (
                <li key={month.key} className="flex items-center justify-between gap-3 py-1.5">
                  <span className="font-medium">{month.label}</span>
                  <span className="text-muted-foreground">
                    {formatNumber(month.sent)} پیامک، {formatNumber(month.cameBack)} برگشت
                  </span>
                  <span className="font-semibold tabular-nums">{formatPercent(month.rate)}</span>
                </li>
              ))}
            </ul>
          </section>

          {notMeasurable > 0 && (
            <p className="text-xs text-muted-foreground">«قابل سنجش نیست»: {notMeasurableRule}</p>
          )}

          <AbsenceSmsNamesDialog effect={effect} />
        </div>
      )}
    </ListFrame>
  );
}

function Figure({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border px-2 py-1.5">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-semibold tabular-nums">{value}</dd>
    </div>
  );
}
