import { useSearchParams } from "react-router";

import { JalaliCalendarField, SelectField } from "@/components/FormField";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { hasRole, useCurrentUser } from "@/features/auth/api";
import { paymentMethodLabels, paymentMethods, type PaymentMethod } from "@/features/payments/api";
import { errorMessage, errorMessages } from "@/lib/errors";
import { gymToday, toPersianDigits } from "@/lib/format";
import { dateFromParams, pageFromParams } from "@/lib/searchParams";

import {
  paymentSourceFilterLabels,
  paymentSourceFilters,
  salePaidFilterLabels,
  salePaidFilters,
  useAttendanceHistory,
  usePaymentHistory,
  usePaymentTotals,
  useSalesHistory,
  useSalesTotals,
  useServiceChargeHistory,
  type HistoryFilter,
  type PaymentSourceFilter,
  type SalePaidFilter,
  type SaleSource,
} from "../api";
import { AttendanceLogTable } from "../components/AttendanceLogTable";
import { HistoryTotals } from "../components/HistoryTotals";
import { MemberFilter } from "../components/MemberFilter";
import { PaymentLogTable } from "../components/PaymentLogTable";
import { SalesLogTable } from "../components/SalesLogTable";
import { ServiceChargeLogTable } from "../components/ServiceChargeLogTable";
import { staffEarliestPaymentDay, staffPaymentDaysBeforeToday } from "../range";

type SalesTab =
  | "sales"
  | "sales-plans"
  | "sales-cardio"
  | "sales-shop"
  | "sales-analysis"
  | "sales-other"
  | "sales-cafe";

type Tab = "attendance" | "payments" | "cardio" | SalesTab;

interface TabItem {
  value: Tab;
  label: string;
}

const attendanceTab: TabItem = { value: "attendance", label: "ورود و خروج" };
const paymentsTab: TabItem = { value: "payments", label: "پرداخت‌ها" };

/** What Staff see: the three sections the page had before the sales sections (BUSINESS_RULES.md §12). */
const staffTabs: TabItem[] = [
  attendanceTab,
  paymentsTab,
  { value: "cardio", label: "هوازی، فروشگاه، آنالیز و متفرقه" },
];

/**
 * Which sale each of the Owner's sales sections lists (§12 Sales in the history). «همهٔ فروش‌ها»
 * names none, so the API sends every kind.
 */
const salesTabSources: Record<SalesTab, SaleSource | undefined> = {
  sales: undefined,
  "sales-plans": "Subscription",
  "sales-cardio": "Cardio",
  "sales-shop": "Miscellaneous",
  "sales-analysis": "Analysis",
  "sales-other": "Other",
  "sales-cafe": "CafeOrder",
};

/** What the Owner sees: the separate sales sections take the place of the combined one. */
const ownerTabs: TabItem[] = [
  attendanceTab,
  paymentsTab,
  { value: "sales", label: "همهٔ فروش‌ها" },
  { value: "sales-plans", label: "فروش پلن" },
  { value: "sales-cardio", label: "هوازی" },
  { value: "sales-shop", label: "فروشگاه" },
  { value: "sales-analysis", label: "آنالیز" },
  { value: "sales-other", label: "متفرقه" },
  { value: "sales-cafe", label: "بوفه" },
];

function isSalesTab(tab: Tab): tab is SalesTab {
  return tab in salesTabSources;
}

/** Everything the URL holds. Unknown values read as "not set", so a stale link still opens. */
interface PageState {
  tab: Tab;
  from: string | undefined;
  to: string | undefined;
  memberId: string | undefined;
  method: PaymentMethod | undefined;
  source: PaymentSourceFilter | undefined;
  paid: SalePaidFilter | undefined;
  page: number;
}

/** A section the signed-in role does not have, or not yet known, opens on the check-ins. */
function tabFrom(params: URLSearchParams, tabs: TabItem[]): Tab {
  const value = params.get("tab");

  return tabs.find((tab) => tab.value === value)?.value ?? "attendance";
}

/**
 * A date missing from the URL is today: every section opens on today. One that is present but
 * empty (`?from=`) was cleared on purpose and is no bound on that side.
 */
function dateFrom(params: URLSearchParams, name: string, today: string): string | undefined {
  return params.has(name) ? dateFromParams(params, name) : today;
}

function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((item) => item === value);
}

/**
 * The gym's history, «تاریخچه» (BUSINESS_RULES.md §12 History, roadmap 6.5.25): every check-in,
 * payment and هوازی, newest first, with who recorded it. The cafe keeps its own order history and
 * is linked from here.
 *
 * The Owner also has the sales sections (§12 Sales in the history, roadmap 6.5.30): everything
 * sold in one list, and each kind on its own, with a «پرداخت شده / پرداخت نشده» choice. Under the
 * sales and the payments, the Owner reads what every row of every page comes to (§12 Totals in the
 * history, roadmap 6.5.32).
 *
 * Every filter lives in the URL (`/history?tab=payments&from=2026-09-29&member=…&page=2`), like the
 * cafe's order history, so a reload, the back button or a shared link opens the same rows.
 *
 * Staff see payments of today and the 3 days before it only. The API refuses anything earlier;
 * the page says so under the date box instead of asking.
 */
export function HistoryPage() {
  const [params, setParams] = useSearchParams();
  const currentUser = useCurrentUser();
  const isOwner = hasRole(currentUser.data, "Owner");
  const tabs = isOwner ? ownerTabs : staffTabs;
  const today = gymToday();

  const state: PageState = {
    // Until the role is known, any section's link is kept rather than sent back to the check-ins.
    tab: tabFrom(params, currentUser.isSuccess ? tabs : [...ownerTabs, ...staffTabs]),
    from: dateFrom(params, "from", today),
    to: dateFrom(params, "to", today),
    memberId: params.get("member") || undefined,
    method: oneOf(params.get("method"), paymentMethods),
    source: oneOf(params.get("source"), paymentSourceFilters),
    paid: oneOf(params.get("paid"), salePaidFilters),
    page: pageFromParams(params),
  };

  /** Writes the next state into the URL. Paging pushes a history entry; filters replace it. */
  function update(next: Partial<PageState>) {
    const merged = { ...state, page: 1, ...next };
    const query: Record<string, string> = {};
    if (merged.tab !== "attendance") query.tab = merged.tab;
    // Today is the default, so it stays out of the URL; a cleared box is kept as empty.
    if (merged.from !== today) query.from = merged.from ?? "";
    if (merged.to !== today) query.to = merged.to ?? "";
    if (merged.memberId !== undefined) query.member = merged.memberId;
    if (merged.method !== undefined) query.method = merged.method;
    if (merged.source !== undefined) query.source = merged.source;
    if (merged.paid !== undefined) query.paid = merged.paid;
    if (merged.page > 1) query.page = String(merged.page);
    setParams(query, { replace: next.page === undefined });
  }

  const rangeIsValid = state.from === undefined || state.to === undefined || state.from <= state.to;
  const staffEarliest = staffEarliestPaymentDay(today);
  const beyondStaffWindow =
    state.tab === "payments" &&
    currentUser.isSuccess &&
    !isOwner &&
    (state.from === undefined || state.from < staffEarliest);

  const fromError = beyondStaffWindow ? errorMessages["Payments.HistoryTooFarBack"] : undefined;
  const toError = rangeIsValid ? undefined : errorMessages["Payments.InvalidDateRange"];

  const filter: HistoryFilter = {
    from: state.from,
    to: state.to,
    memberId: state.memberId,
    page: state.page,
  };
  // The role decides what may be asked for payments, so they wait until it is known.
  const canAsk = rangeIsValid && !beyondStaffWindow;
  const attendance = useAttendanceHistory(filter, {
    enabled: canAsk && state.tab === "attendance",
  });
  const payments = usePaymentHistory(
    { ...filter, method: state.method, source: state.source },
    { enabled: canAsk && state.tab === "payments" && currentUser.isSuccess },
  );
  const cardio = useServiceChargeHistory(filter, { enabled: canAsk && state.tab === "cardio" });
  const salesFilter = {
    from: state.from,
    to: state.to,
    memberId: state.memberId,
    source: isSalesTab(state.tab) ? salesTabSources[state.tab] : undefined,
    paid: state.paid,
  };
  const sales = useSalesHistory(
    { ...salesFilter, page: state.page },
    { enabled: canAsk && isSalesTab(state.tab) && isOwner },
  );
  // Totals leave the page out, so turning a page does not ask for them again.
  const salesTotals = useSalesTotals(salesFilter, {
    enabled: canAsk && isSalesTab(state.tab) && isOwner,
  });
  const paymentTotals = usePaymentTotals(
    {
      from: state.from,
      to: state.to,
      memberId: state.memberId,
      method: state.method,
      source: state.source,
    },
    { enabled: canAsk && state.tab === "payments" && isOwner },
  );

  const active =
    state.tab === "attendance"
      ? attendance
      : state.tab === "payments"
        ? payments
        : state.tab === "cardio"
          ? cardio
          : sales;

  return (
    <div className="space-y-4">
      {/* No link to «سفارش‌های بوفه» here: the menu and the cafe till already have one. */}
      <h2 className="text-xl font-bold">تاریخچه</h2>

      <Card>
        <CardContent className="space-y-4">
          <div
            role="tablist"
            aria-label="بخش‌های تاریخچه"
            className="flex flex-wrap gap-1 border-b"
          >
            {tabs.map((item) => (
              <Button
                key={item.value}
                role="tab"
                aria-selected={state.tab === item.value}
                size="sm"
                variant={state.tab === item.value ? "secondary" : "ghost"}
                onClick={() => update({ tab: item.value })}
              >
                {item.label}
              </Button>
            ))}
          </div>

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <JalaliCalendarField
              label="از تاریخ"
              value={state.from ?? ""}
              error={fromError}
              onChange={(iso) => update({ from: iso === "" ? undefined : iso })}
            />
            <JalaliCalendarField
              label="تا تاریخ"
              value={state.to ?? ""}
              error={toError}
              onChange={(iso) => update({ to: iso === "" ? undefined : iso })}
            />
            <div className="sm:col-span-2">
              <MemberFilter
                memberId={state.memberId}
                onChange={(memberId) => update({ memberId })}
              />
            </div>
            {state.tab === "payments" && (
              <>
                <SelectField
                  label="روش پرداخت"
                  value={state.method ?? ""}
                  onChange={(event) =>
                    update({ method: oneOf(event.target.value, paymentMethods) })
                  }
                >
                  <option value="">همه</option>
                  {paymentMethods.map((method) => (
                    <option key={method} value={method}>
                      {paymentMethodLabels[method]}
                    </option>
                  ))}
                </SelectField>
                <SelectField
                  label="بابت"
                  value={state.source ?? ""}
                  onChange={(event) =>
                    update({ source: oneOf(event.target.value, paymentSourceFilters) })
                  }
                >
                  <option value="">همه</option>
                  {paymentSourceFilters.map((source) => (
                    <option key={source} value={source}>
                      {paymentSourceFilterLabels[source]}
                    </option>
                  ))}
                </SelectField>
              </>
            )}
            {isSalesTab(state.tab) && (
              <div className="space-y-1 sm:col-span-2">
                <p id="paid-filter" className="text-sm font-medium">
                  وضعیت پرداخت
                </p>
                <div role="group" aria-labelledby="paid-filter" className="flex gap-1">
                  {[undefined, ...salePaidFilters].map((paid) => (
                    <Button
                      key={paid ?? "all"}
                      type="button"
                      size="sm"
                      aria-pressed={state.paid === paid}
                      variant={state.paid === paid ? "secondary" : "outline"}
                      onClick={() => update({ paid })}
                    >
                      {paid === undefined ? "همه" : salePaidFilterLabels[paid]}
                    </Button>
                  ))}
                </div>
              </div>
            )}
          </div>

          {state.tab === "payments" && currentUser.isSuccess && !isOwner && (
            <p className="text-sm text-muted-foreground">
              پرداخت‌های امروز و {toPersianDigits(staffPaymentDaysBeforeToday)} روز قبل از آن را
              می‌بینید.
            </p>
          )}

          {canAsk && (
            <>
              {active.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
              {active.isError && <Alert variant="destructive">{errorMessage(active.error)}</Alert>}
              {active.isSuccess && (
                <p className="text-sm text-muted-foreground">
                  {toPersianDigits(active.data.totalCount)} ردیف
                </p>
              )}
              {active.isSuccess && active.data.totalCount === 0 && (
                <p className="text-muted-foreground">در این بازه چیزی ثبت نشده است.</p>
              )}
              {state.tab === "attendance" &&
                attendance.isSuccess &&
                attendance.data.totalCount > 0 && (
                  <AttendanceLogTable items={attendance.data.items} />
                )}
              {state.tab === "payments" && payments.isSuccess && payments.data.totalCount > 0 && (
                <PaymentLogTable items={payments.data.items} />
              )}
              {state.tab === "payments" &&
                isOwner &&
                payments.isSuccess &&
                payments.data.totalCount > 0 &&
                paymentTotals.isSuccess && (
                  <HistoryTotals
                    totals={[
                      { label: "دریافتی", amount: paymentTotals.data.received },
                      { label: "بازگشت", amount: paymentTotals.data.refunded },
                      { label: "خالص", amount: paymentTotals.data.net },
                    ]}
                  />
                )}
              {state.tab === "cardio" && cardio.isSuccess && cardio.data.totalCount > 0 && (
                <ServiceChargeLogTable items={cardio.data.items} />
              )}
              {isSalesTab(state.tab) && sales.isSuccess && sales.data.totalCount > 0 && (
                <SalesLogTable items={sales.data.items} />
              )}
              {isSalesTab(state.tab) &&
                sales.isSuccess &&
                sales.data.totalCount > 0 &&
                salesTotals.isSuccess && (
                  <HistoryTotals
                    totals={[
                      { label: "مبلغ", amount: salesTotals.data.amount },
                      { label: "دریافتی", amount: salesTotals.data.netPaid },
                      { label: "مانده", amount: salesTotals.data.remaining },
                    ]}
                  />
                )}
              {active.isSuccess && active.data.totalCount > 0 && (
                <Pager
                  page={state.page}
                  pageCount={active.data.pageCount}
                  onPageChange={(page) => update({ page })}
                />
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
