import { Link, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { JalaliDateField, SelectField } from "@/components/FormField";
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
  paymentSourceLabels,
  paymentSources,
  useAttendanceHistory,
  usePaymentHistory,
  useServiceChargeHistory,
  type HistoryFilter,
  type PaymentSource,
} from "../api";
import { AttendanceLogTable } from "../components/AttendanceLogTable";
import { MemberFilter } from "../components/MemberFilter";
import { PaymentLogTable } from "../components/PaymentLogTable";
import { ServiceChargeLogTable } from "../components/ServiceChargeLogTable";
import { staffEarliestPaymentDay, staffPaymentDaysBeforeToday } from "../range";

type Tab = "attendance" | "payments" | "cardio";

const tabs: { value: Tab; label: string }[] = [
  { value: "attendance", label: "ورود و خروج" },
  { value: "payments", label: "پرداخت‌ها" },
  { value: "cardio", label: "هوازی" },
];

/** Everything the URL holds. Unknown values read as "not set", so a stale link still opens. */
interface PageState {
  tab: Tab;
  from: string | undefined;
  to: string | undefined;
  memberId: string | undefined;
  method: PaymentMethod | undefined;
  source: PaymentSource | undefined;
  page: number;
}

function tabFrom(params: URLSearchParams): Tab {
  const value = params.get("tab");

  return tabs.some((tab) => tab.value === value) ? (value as Tab) : "attendance";
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
  const today = gymToday();

  const state: PageState = {
    tab: tabFrom(params),
    from: dateFrom(params, "from", today),
    to: dateFrom(params, "to", today),
    memberId: params.get("member") || undefined,
    method: oneOf(params.get("method"), paymentMethods),
    source: oneOf(params.get("source"), paymentSources),
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

  const active =
    state.tab === "attendance" ? attendance : state.tab === "payments" ? payments : cardio;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">تاریخچه</h2>
        <Button asChild size="sm" variant="outline">
          <Link to={paths.cafeOrders}>سفارش‌های بوفه</Link>
        </Button>
      </div>

      <Card>
        <CardContent className="space-y-4">
          <div role="tablist" aria-label="بخش‌های تاریخچه" className="flex gap-1 border-b">
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
            <JalaliDateField
              label="از تاریخ"
              value={state.from ?? ""}
              error={fromError}
              onChange={(iso) => update({ from: iso === "" ? undefined : iso })}
            />
            <JalaliDateField
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
                    update({ source: oneOf(event.target.value, paymentSources) })
                  }
                >
                  <option value="">همه</option>
                  {paymentSources.map((source) => (
                    <option key={source} value={source}>
                      {paymentSourceLabels[source]}
                    </option>
                  ))}
                </SelectField>
              </>
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
              {state.tab === "cardio" && cardio.isSuccess && cardio.data.totalCount > 0 && (
                <ServiceChargeLogTable items={cardio.data.items} />
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
