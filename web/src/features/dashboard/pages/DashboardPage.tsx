import { useSearchParams } from "react-router";

import { Alert } from "@/components/ui/alert";
import { Card, CardContent } from "@/components/ui/card";
import { paymentMethodLabels } from "@/features/payments/api";
import { errorMessage, errorMessages } from "@/lib/errors";
import { formatMoney, formatNumber, formatPercent, gymToday, toPersianDigits } from "@/lib/format";
import { dateFromParams } from "@/lib/searchParams";

import {
  reportThresholds,
  revenueSourceLabels,
  useAttendanceReport,
  useFinancialReport,
  useMembersReport,
  useNeedsAttention,
  useReceivables,
  useSubscriptionsSnapshot,
  useTopCafeProducts,
  type FinancialPeriod,
} from "../api";
import { AttendanceHeatmap } from "../components/AttendanceHeatmap";
import { BarListChart } from "../components/BarListChart";
import { MonthlyChart } from "../components/MonthlyChart";
import { NeedsAttentionPanel } from "../components/NeedsAttentionPanel";
import { RangePicker, type RangeDraft } from "../components/RangePicker";
import { RevenueChart } from "../components/RevenueChart";
import { StaffMoneyTable } from "../components/StaffMoneyTable";
import { StatCard } from "../components/StatCard";
import { byJalaliMonth, renewalRate } from "../figures";
import { defaultRangePreset, presetRange, rangeError, type ReportRange } from "../range";

/**
 * The range from the URL (`?from=2026-09-23&to=2026-10-04`). With neither date the dashboard opens
 * on this Jalali month; a date that is present but empty or hand-edited into nonsense is missing,
 * and the page says so instead of asking.
 */
function rangeFrom(params: URLSearchParams, today: string): RangeDraft {
  if (!params.has("from") && !params.has("to")) {
    return presetRange(defaultRangePreset, today);
  }

  return { from: dateFromParams(params, "from"), to: dateFromParams(params, "to") };
}

function cashNet(period: FinancialPeriod): number | string {
  return period.byMethod.find((item) => item.method === "Cash")?.money.net ?? 0;
}

const formatCount = (value: number) => formatNumber(value);

/**
 * The Owner's dashboard, «داشبورد» (BUSINESS_RULES.md §12 *Dashboard*, roadmap 9.3): the range's
 * money and visits beside the range before, the plans and debts of today, who to call, and the
 * charts. Every figure comes from the reports of 9.1 and 9.2; nothing is added up here except days
 * into Jalali months.
 *
 * The range lives in the URL like the history's filters, so a reload or a shared link opens the
 * same figures. What does not depend on the range (plans today, receivables, needs attention) is
 * asked for once and stays when the range changes.
 */
export function DashboardPage() {
  const [params, setParams] = useSearchParams();
  const today = gymToday();
  const draft = rangeFrom(params, today);
  const errorCode = rangeError(draft);
  const canAsk = errorCode === undefined;
  // Only read while canAsk, when both dates are there.
  const range = draft as ReportRange;

  function changeRange(next: RangeDraft) {
    const isDefault =
      next.from !== undefined &&
      next.to !== undefined &&
      next.from === presetRange(defaultRangePreset, today).from &&
      next.to === presetRange(defaultRangePreset, today).to;
    setParams(isDefault ? {} : { from: next.from ?? "", to: next.to ?? "" }, { replace: true });
  }

  const financial = useFinancialReport(range, { enabled: canAsk });
  const attendance = useAttendanceReport(range, { enabled: canAsk });
  const members = useMembersReport(range, { enabled: canAsk });
  const cafeProducts = useTopCafeProducts(range, { enabled: canAsk });
  const receivables = useReceivables();
  const plans = useSubscriptionsSnapshot();
  const attention = useNeedsAttention();

  const failed = [financial, attendance, members, cafeProducts, receivables, plans, attention].find(
    (query) => query.isError,
  );

  const months = members.isSuccess ? byJalaliMonth(members.data.days) : [];

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-bold">داشبورد</h2>

      <Card>
        <CardContent>
          <RangePicker
            range={draft}
            today={today}
            onChange={changeRange}
            error={errorCode === undefined ? undefined : errorMessages[errorCode]}
          />
        </CardContent>
      </Card>

      {failed !== undefined && <Alert variant="destructive">{errorMessage(failed.error)}</Alert>}
      {canAsk && financial.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}

      {canAsk && (financial.isSuccess || attendance.isSuccess || members.isSuccess) && (
        <section aria-labelledby="range-summary" className="space-y-2">
          <h3 id="range-summary" className="font-semibold">
            خلاصهٔ بازه
          </h3>
          <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {financial.isSuccess && (
              <>
                <StatCard
                  label="درآمد خالص"
                  value={formatMoney(financial.data.current.revenue.net)}
                  hint={`فروش بازه: ${formatMoney(financial.data.current.sales)}`}
                  comparison={{
                    current: financial.data.current.revenue.net,
                    previous: financial.data.previous.revenue.net,
                    previousLabel: formatMoney(financial.data.previous.revenue.net),
                  }}
                />
                <StatCard
                  label="هزینه‌ها"
                  value={formatMoney(financial.data.current.expenses)}
                  comparison={{
                    current: financial.data.current.expenses,
                    previous: financial.data.previous.expenses,
                    previousLabel: formatMoney(financial.data.previous.expenses),
                    higherIsBetter: false,
                  }}
                />
                <StatCard
                  label="سود خالص"
                  value={formatMoney(financial.data.current.netProfit)}
                  hint="درآمد خالص منهای هزینه‌ها"
                  comparison={{
                    current: financial.data.current.netProfit,
                    previous: financial.data.previous.netProfit,
                    previousLabel: formatMoney(financial.data.previous.netProfit),
                  }}
                />
                <StatCard
                  label="فروش"
                  value={formatMoney(financial.data.current.sales)}
                  hint="آنچه در بازه فروخته شده، پرداخت شده یا نشده"
                  comparison={{
                    current: financial.data.current.sales,
                    previous: financial.data.previous.sales,
                    previousLabel: formatMoney(financial.data.previous.sales),
                  }}
                />
                <StatCard
                  label="صندوق نقدی"
                  value={formatMoney(cashNet(financial.data.current))}
                  hint="دریافت نقدی منهای بازگشت نقدی"
                  comparison={{
                    current: cashNet(financial.data.current),
                    previous: cashNet(financial.data.previous),
                    previousLabel: formatMoney(cashNet(financial.data.previous)),
                  }}
                />
                <StatCard
                  label="سود ناخالص بوفه"
                  value={formatMoney(financial.data.current.cafeGrossProfit)}
                  hint="درآمد بوفه منهای هزینهٔ «خرید بوفه»"
                  comparison={{
                    current: financial.data.current.cafeGrossProfit,
                    previous: financial.data.previous.cafeGrossProfit,
                    previousLabel: formatMoney(financial.data.previous.cafeGrossProfit),
                  }}
                />
              </>
            )}
            {attendance.isSuccess && (
              <StatCard
                label="ورود اعضا"
                value={formatNumber(Number(attendance.data.visits))}
                hint={`${formatNumber(Number(attendance.data.members))} عضو مختلف`}
                comparison={{
                  current: attendance.data.visits,
                  previous: attendance.data.previousVisits,
                  previousLabel: formatNumber(Number(attendance.data.previousVisits)),
                }}
              />
            )}
            {members.isSuccess && (
              <>
                <StatCard
                  label="اعضای جدید"
                  value={formatNumber(Number(members.data.newMembers))}
                  hint="با اولین پلن عضویتشان"
                />
                <RenewalCard
                  renewed={Number(members.data.renewed)}
                  ended={Number(members.data.ended)}
                  waiting={Number(members.data.waiting)}
                />
              </>
            )}
          </dl>
        </section>
      )}

      {(plans.isSuccess || receivables.isSuccess) && (
        <section aria-labelledby="today-summary" className="space-y-2">
          <h3 id="today-summary" className="font-semibold">
            امروز
          </h3>
          <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {plans.isSuccess && (
              <>
                <StatCard label="پلن فعال" value={formatNumber(Number(plans.data.active))} />
                <StatCard label="پلن فریز" value={formatNumber(Number(plans.data.frozen))} />
                <StatCard
                  label="رو به پایان"
                  value={formatNumber(Number(plans.data.expiringSoon))}
                  hint={`تا ${toPersianDigits(reportThresholds.expiringWithinDays)} روز دیگر تمام می‌شود`}
                />
                <StatCard
                  label="کم‌جلسه"
                  value={formatNumber(Number(plans.data.lowSessions))}
                  hint={`${toPersianDigits(reportThresholds.lowSessions)} جلسه یا کمتر مانده`}
                />
              </>
            )}
            {receivables.isSuccess && (
              <>
                <StatCard
                  label="کل مطالبات"
                  value={formatMoney(receivables.data.total)}
                  hint="آنچه همه تا امروز بدهکارند"
                />
                <StatCard
                  label="مطالبات ۰ تا ۷ روزه"
                  value={formatMoney(receivables.data.upTo7Days)}
                />
                <StatCard
                  label="مطالبات ۸ تا ۳۰ روزه"
                  value={formatMoney(receivables.data.from8To30Days)}
                />
                <StatCard
                  label="مطالبات بیش از ۳۰ روز"
                  value={formatMoney(receivables.data.over30Days)}
                />
              </>
            )}
          </dl>
        </section>
      )}

      {attention.isSuccess && <NeedsAttentionPanel data={attention.data} />}

      {canAsk && (
        <div className="grid gap-4 lg:grid-cols-2">
          {financial.isSuccess && (
            <>
              <RevenueChart days={financial.data.days} />
              <BarListChart
                title="درآمد به تفکیک منبع"
                description="خالص دریافتی، به تومان"
                items={financial.data.current.bySource.map((item) => ({
                  key: item.source,
                  name: revenueSourceLabels[item.source],
                  value: Number(item.money.net),
                }))}
                formatValue={formatMoney}
              />
              <BarListChart
                title="درآمد به تفکیک روش پرداخت"
                description="خالص دریافتی، به تومان"
                items={financial.data.current.byMethod.map((item) => ({
                  key: item.method,
                  name: paymentMethodLabels[item.method],
                  value: Number(item.money.net),
                }))}
                formatValue={formatMoney}
              />
              <BarListChart
                title="هزینه به تفکیک دسته"
                description="هزینه‌های باطل‌شده حساب نمی‌شوند، به تومان"
                color="var(--chart-2)"
                items={financial.data.current.expensesByCategory.map((item) => ({
                  key: item.categoryId,
                  name: item.name,
                  value: Number(item.amount),
                }))}
                formatValue={formatMoney}
              />
            </>
          )}
          {cafeProducts.isSuccess && (
            <BarListChart
              title="پرفروش‌ترین محصولات بوفه"
              description="۱۰ محصول، به تعداد"
              items={cafeProducts.data.map((item) => ({
                key: item.productId,
                name: item.name,
                value: Number(item.quantity),
                detail: formatMoney(item.amount),
              }))}
              formatValue={(value) => `${formatNumber(value)} عدد`}
            />
          )}
          {members.isSuccess && (
            <>
              <MonthlyChart
                title="اعضای جدید در هر ماه"
                description="با اولین پلن عضویتشان"
                valueName="اعضای جدید"
                points={months.map((month) => ({
                  key: month.key,
                  label: month.label,
                  value: month.newMembers,
                }))}
                formatValue={formatCount}
              />
              <MonthlyChart
                title="نرخ تمدید در هر ماه"
                description={`پلن‌های تمام‌شده که تا ${toPersianDigits(reportThresholds.windowDays)} روز بعد تمدید شدند`}
                valueName="نرخ تمدید"
                max={100}
                points={months.map((month) => ({
                  key: month.key,
                  label: month.label,
                  value: month.renewalRate,
                  details: [
                    { name: "تمام‌شده", value: formatNumber(month.ended) },
                    { name: "تمدیدشده", value: formatNumber(month.renewed) },
                    { name: "در انتظار", value: formatNumber(month.waiting) },
                  ],
                }))}
                formatValue={formatPercent}
              />
            </>
          )}
          {attendance.isSuccess && <AttendanceHeatmap report={attendance.data} />}
          {financial.isSuccess && <StaffMoneyTable rows={financial.data.current.byStaff} />}
        </div>
      )}
    </div>
  );
}

/**
 * Renewed ÷ (ended − waiting) of the range (§12 *Operational reports*). Plans still inside their
 * 30 days are counted apart, so the Owner sees why the rate rests on fewer plans than ended.
 */
function RenewalCard({
  renewed,
  ended,
  waiting,
}: {
  renewed: number;
  ended: number;
  waiting: number;
}) {
  const rate = renewalRate(renewed, ended, waiting);
  const decided = ended - waiting;

  return (
    <StatCard
      label="نرخ تمدید"
      value={formatPercent(rate)}
      hint={
        rate === null
          ? waiting > 0
            ? `${formatNumber(waiting)} پلن تمام‌شده هنوز فرصت تمدید دارد`
            : "در این بازه پلنی تمام نشده است"
          : `${formatNumber(renewed)} از ${formatNumber(decided)} پلن تمام‌شده؛ ${formatNumber(waiting)} در انتظار`
      }
    />
  );
}
