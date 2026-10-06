import {
  Activity,
  ArrowLeftRight,
  BatteryLow,
  CalendarCheck,
  ChartColumn,
  ChartPie,
  CircleCheck,
  Coffee,
  CreditCard,
  Footprints,
  Hourglass,
  IdCard,
  Receipt,
  Repeat,
  ShoppingBag,
  Snowflake,
  Store,
  Ticket,
  TrendingUp,
  UserPlus,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import { useSearchParams } from "react-router";

import { Alert } from "@/components/ui/alert";
import { paymentMethodLabels, type PaymentMethod } from "@/features/payments/api";
import { errorMessage, errorMessages } from "@/lib/errors";
import { formatMoney, formatNumber, formatPercent, gymToday, toPersianDigits } from "@/lib/format";
import { dateFromParams } from "@/lib/searchParams";

import {
  reportThresholds,
  revenueSourceLabels,
  revenueSourceOrder,
  soldCountNouns,
  useAttendanceReport,
  useFinancialReport,
  useMembersReport,
  useNeedsAttention,
  useReceivables,
  useSubscriptionsSnapshot,
  useTopCafeProducts,
  type FinancialPeriod,
  type FinancialReport,
} from "../api";
import { AttendanceHeatmap } from "../components/AttendanceHeatmap";
import { BarListChart, type BarListItem } from "../components/BarListChart";
import { DashboardHero } from "../components/DashboardHero";
import { MonthlyChart } from "../components/MonthlyChart";
import { NeedsAttentionPanel } from "../components/NeedsAttentionPanel";
import { RangePicker, type RangeDraft } from "../components/RangePicker";
import { ReceivablesCard } from "../components/ReceivablesCard";
import { RevenueChart } from "../components/RevenueChart";
import { SectionHeading } from "../components/SectionHeading";
import { StaffMoneyTable } from "../components/StaffMoneyTable";
import { StatCard, StatCardsLoading } from "../components/StatCard";
import { byJalaliMonth, conversionRate, methodsTotal, outcomeOf, renewalRate } from "../figures";
import { defaultRangePreset, presetRange, rangeError, type ReportRange } from "../range";
import type { Tone } from "../tone";

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

const formatCount = (value: number) => formatNumber(value);

/** «فروش» leaves فروشگاه and آنالیز out, and says so wherever it is shown (§12, 1405/07/14). */
const salesLabel = "فروش (به غیر از آنالیز و فروشگاه)";

/**
 * The methods on the «دریافتی» cards, in the developer's own short words (1405/07/14): the card's
 * corner has room for one word each.
 */
const methodLabels: Record<PaymentMethod, string> = {
  Card: "کارت",
  BankTransfer: "انتقال",
  Cash: "نقد",
};

/**
 * The sources in the dashboard's order (cafe above فروشگاه), each bar the money received, and on
 * the plan and single-visit bars how many were sold in the range (§12 *Financial report*).
 */
function bySourceItems(period: FinancialPeriod): BarListItem[] {
  return revenueSourceOrder.flatMap((source) => {
    const row = period.bySource.find((item) => item.source === source);
    if (row === undefined) {
      return [];
    }

    const noun = soldCountNouns[source];
    return [
      {
        key: source,
        name: revenueSourceLabels[source],
        value: Number(row.money.net),
        sold: noun === undefined ? undefined : { count: Number(row.sold), noun },
      },
    ];
  });
}

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
    <div className="space-y-8">
      <DashboardHero range={draft}>
        <RangePicker
          range={draft}
          today={today}
          onChange={changeRange}
          error={errorCode === undefined ? undefined : errorMessages[errorCode]}
        />
      </DashboardHero>

      {failed !== undefined && <Alert variant="destructive">{errorMessage(failed.error)}</Alert>}
      {canAsk && financial.isPending && <StatCardsLoading />}

      {canAsk && (financial.isSuccess || attendance.isSuccess || members.isSuccess) && (
        <section aria-labelledby="range-summary" className="space-y-3">
          <SectionHeading id="range-summary" icon={ChartColumn} tone="blue">
            خلاصهٔ بازه
          </SectionHeading>
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {financial.isSuccess && (
              <>
                <StatCard
                  label={salesLabel}
                  tone="violet"
                  icon={ShoppingBag}
                  value={formatMoney(financial.data.current.sales)}
                  hint="آنچه در بازه فروخته شده، پرداخت شده یا نشده"
                  comparison={{
                    current: financial.data.current.sales,
                    previous: financial.data.previous.sales,
                    previousLabel: formatMoney(financial.data.previous.sales),
                  }}
                  breakdown={[
                    { label: "پرداخت‌شده", value: formatMoney(financial.data.current.salesPaid) },
                    { label: "نسیه", value: formatMoney(financial.data.current.salesOwed) },
                  ]}
                />
                <ReceivedCard
                  label="دریافتی"
                  tone="blue"
                  icon={Wallet}
                  hint="آنچه در بازه آمد، بدهی‌های قبلی هم، منهای بازگشت‌ها؛ بدون آنالیز و فروشگاه"
                  current={financial.data.current.receivedByMethod}
                  previous={financial.data.previous.receivedByMethod}
                />
                <ReceivedCard
                  label="دریافتی آنالیز و فروشگاه"
                  tone="amber"
                  icon={Store}
                  hint="پول آنالیز و فروشگاه، جدا از دریافتی باشگاه"
                  current={financial.data.current.shopAndAnalysisByMethod}
                  previous={financial.data.previous.shopAndAnalysisByMethod}
                />
                <StatCard
                  label="هزینه‌ها"
                  tone="orange"
                  icon={Receipt}
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
                  tone="green"
                  icon={TrendingUp}
                  value={formatMoney(financial.data.current.netProfit)}
                  outcome={outcomeOf(financial.data.current.netProfit)}
                  hint="درآمد به غیر از آنالیز و فروشگاه، منهای همهٔ هزینه‌ها"
                  comparison={{
                    current: financial.data.current.netProfit,
                    previous: financial.data.previous.netProfit,
                    previousLabel: formatMoney(financial.data.previous.netProfit),
                  }}
                />
                <StatCard
                  label="سود بوفه"
                  tone="brown"
                  icon={Coffee}
                  value={formatMoney(financial.data.current.cafeGrossProfit)}
                  outcome={outcomeOf(financial.data.current.cafeGrossProfit)}
                  hint="درآمد بوفه منهای هزینهٔ «خرید بوفه»"
                  comparison={{
                    current: financial.data.current.cafeGrossProfit,
                    previous: financial.data.previous.cafeGrossProfit,
                    previousLabel: formatMoney(financial.data.previous.cafeGrossProfit),
                  }}
                />
                <SourceCard
                  label="خرید پلن"
                  tone="indigo"
                  icon={IdCard}
                  source="Membership"
                  report={financial.data}
                />
                <SourceCard
                  label="تک‌جلسه‌ای"
                  tone="lime"
                  icon={Ticket}
                  source="SingleSession"
                  report={financial.data}
                />
              </>
            )}
            {attendance.isSuccess && (
              <StatCard
                label="ورود اعضا"
                tone="sky"
                icon={Footprints}
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
                  tone="pink"
                  icon={UserPlus}
                  value={formatNumber(Number(members.data.newMembers))}
                  hint="عضوهایی که اولین پلن عضویتشان در این بازه فروخته شده"
                />
                <RenewalCard
                  renewed={Number(members.data.renewed)}
                  ended={Number(members.data.ended)}
                  waiting={Number(members.data.waiting)}
                />
                <ConversionCard
                  converted={Number(members.data.trialsConverted)}
                  trials={Number(members.data.trials)}
                  waiting={Number(members.data.trialsWaiting)}
                />
              </>
            )}
          </dl>
        </section>
      )}

      {(plans.isSuccess || receivables.isSuccess) && (
        <section aria-labelledby="today-summary" className="space-y-3">
          <SectionHeading
            id="today-summary"
            icon={CalendarCheck}
            tone="green"
            aside="پلن‌ها و بدهی‌ها، مستقل از بازهٔ انتخاب‌شده"
          >
            امروز
          </SectionHeading>
          <div className="grid gap-4 xl:grid-cols-2">
            {plans.isSuccess && (
              <dl className="grid gap-4 sm:grid-cols-2">
                <StatCard
                  label="پلن فعال"
                  tone="green"
                  icon={CircleCheck}
                  value={formatNumber(Number(plans.data.active))}
                />
                <StatCard
                  label="پلن فریز"
                  tone="sky"
                  icon={Snowflake}
                  value={formatNumber(Number(plans.data.frozen))}
                />
                <StatCard
                  label="رو به پایان"
                  tone="amber"
                  icon={Hourglass}
                  value={formatNumber(Number(plans.data.expiringSoon))}
                  hint={`تا ${toPersianDigits(reportThresholds.expiringWithinDays)} روز دیگر تمام می‌شود`}
                />
                <StatCard
                  label="کم‌جلسه"
                  tone="orange"
                  icon={BatteryLow}
                  value={formatNumber(Number(plans.data.lowSessions))}
                  hint={`${toPersianDigits(reportThresholds.lowSessions)} جلسه یا کمتر مانده`}
                />
              </dl>
            )}
            {receivables.isSuccess && <ReceivablesCard data={receivables.data} />}
          </div>
        </section>
      )}

      {attention.isSuccess && <NeedsAttentionPanel data={attention.data} />}

      {canAsk &&
        (financial.isSuccess ||
          members.isSuccess ||
          attendance.isSuccess ||
          cafeProducts.isSuccess) && (
          <section aria-labelledby="charts" className="space-y-3">
            <SectionHeading id="charts" icon={Activity} tone="violet">
              نمودارها و جزئیات بازه
            </SectionHeading>
            <div className="grid gap-4 lg:grid-cols-2">
              {financial.isSuccess && (
                <>
                  <RevenueChart days={financial.data.days} />
                  <BarListChart
                    title="درآمد به تفکیک منبع"
                    description="خالص دریافتی به تومان؛ روی پلن و تک‌جلسه‌ای، تعدادی که در این بازه فروخته شده"
                    icon={ChartPie}
                    tone="blue"
                    color="var(--chart-1)"
                    items={bySourceItems(financial.data.current)}
                    formatValue={formatMoney}
                  />
                  <BarListChart
                    title="درآمد به تفکیک روش پرداخت"
                    description="خالص دریافتی، به تومان"
                    icon={CreditCard}
                    tone="teal"
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
                    icon={Receipt}
                    tone="orange"
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
                  icon={Coffee}
                  tone="brown"
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
                    description="عضوهایی که اولین پلن عضویتشان در این ماه فروخته شده"
                    valueName="اعضای جدید"
                    icon={UserPlus}
                    tone="pink"
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
                    icon={Repeat}
                    tone="teal"
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
          </section>
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
      tone="teal"
      icon={Repeat}
      value={formatPercent(rate)}
      progress={rate}
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

/**
 * Money that came in during the range, by method, refunds taken off, written «کارت», «انتقال»,
 * «نقد» in the card's corner (asked by the developer, 1405/07/14). The gym's own «دریافتی» is
 * what the drawer, the card reader and the account should show for the range; فروشگاه and آنالیز
 * have a card of their own, because their money is someone else's.
 */
function ReceivedCard({
  label,
  tone,
  icon,
  hint,
  current,
  previous,
}: {
  label: string;
  tone: Tone;
  icon: LucideIcon;
  hint: string;
  current: FinancialPeriod["byMethod"];
  previous: FinancialPeriod["byMethod"];
}) {
  const total = methodsTotal(current);
  const totalBefore = methodsTotal(previous);

  return (
    <StatCard
      label={label}
      tone={tone}
      icon={icon}
      value={formatMoney(total)}
      hint={hint}
      comparison={{
        current: total,
        previous: totalBefore,
        previousLabel: formatMoney(totalBefore),
      }}
      breakdown={current.map((row) => ({
        label: methodLabels[row.method],
        value: formatMoney(row.money.net),
      }))}
    />
  );
}

/**
 * What one kind of sale sold for in the range, paid or not, by the day it was sold, with how many
 * underneath (asked by the developer, 1405/07/14: «خرید پلن» and «تک‌جلسه‌ای»). By the sale's day,
 * so a single visit sold last week and paid today is not today's: today's money is «دریافتی».
 */
function SourceCard({
  label,
  tone,
  icon,
  source,
  report,
}: {
  label: string;
  tone: Tone;
  icon: LucideIcon;
  source: "Membership" | "SingleSession";
  report: FinancialReport;
}) {
  const current = report.current.bySource.find((row) => row.source === source);
  const previous = report.previous.bySource.find((row) => row.source === source);
  const noun = soldCountNouns[source] ?? "";

  return (
    <StatCard
      label={label}
      tone={tone}
      icon={icon}
      value={formatMoney(current?.soldAmount ?? 0)}
      hint={`${formatNumber(Number(current?.sold ?? 0))} ${noun} در این بازه فروخته شد، پرداخت شده یا نشده`}
      comparison={{
        current: current?.soldAmount ?? 0,
        previous: previous?.soldAmount ?? 0,
        previousLabel: formatMoney(previous?.soldAmount ?? 0),
      }}
    />
  );
}

/**
 * Converted ÷ (trials − waiting) of the range (§12 *Operational reports*, decided with the
 * developer, 1405/07/14): of the new people who came for a single visit, how many bought a plan
 * within 30 days. Like the renewal rate, those still inside their 30 days are counted apart.
 */
function ConversionCard({
  converted,
  trials,
  waiting,
}: {
  converted: number;
  trials: number;
  waiting: number;
}) {
  const rate = conversionRate(converted, trials, waiting);
  const decided = trials - waiting;

  return (
    <StatCard
      label="نرخ تبدیل تک‌جلسه‌ای به پلن"
      tone="cyan"
      icon={ArrowLeftRight}
      value={formatPercent(rate)}
      progress={rate}
      hint={
        rate === null
          ? waiting > 0
            ? `${formatNumber(waiting)} نفر هنوز فرصت خرید پلن دارند`
            : "در این بازه کسی برای اولین بار تک‌جلسه‌ای نخریده است"
          : `${formatNumber(converted)} از ${formatNumber(decided)} نفر تا ${toPersianDigits(reportThresholds.windowDays)} روز بعد پلن خریدند؛ ${formatNumber(waiting)} در انتظار`
      }
    />
  );
}
