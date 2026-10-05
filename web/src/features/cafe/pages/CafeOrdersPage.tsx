import { useState } from "react";
import { Link, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { JalaliCalendarField } from "@/components/FormField";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage, errorMessages } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { dateFromParams, pageFromParams } from "@/lib/searchParams";

import { useCafeOrderList } from "../api";
import { CafeOrdersTable } from "../components/CafeOrdersTable";

/**
 * Every order the till has rung up, newest first, cancelled ones included and marked
 * (BUSINESS_RULES.md §8 Order history). The range is of the order's business date and inclusive
 * at both ends; either end may be left open.
 *
 * The filter lives in the URL (`/cafe/orders?from=2026-09-01&to=2026-09-30&unpaidGuest=true&page=2`), like the
 * member search, so a refresh or the back button returns to the same list. The dates in it are
 * the ISO ones the API reads; the boxes show them in Jalali.
 */
export function CafeOrdersPage() {
  const [params, setParams] = useSearchParams();
  const from = dateFromParams(params, "from");
  const to = dateFromParams(params, "to");
  const page = pageFromParams(params);
  // What the nightly job left behind on a guest's visit (BUSINESS_RULES.md §7 *Guest visit*).
  const unpaidGuest = params.get("unpaidGuest") === "true";
  const [notice, setNotice] = useState<string | null>(null);

  const rangeIsValid = from === undefined || to === undefined || from <= to;
  // A backwards range is said beside the box rather than sent for the API to refuse.
  const orders = useCafeOrderList({ from, to, unpaidGuest, page }, { enabled: rangeIsValid });

  function setFilter(next: { from?: string; to?: string; unpaidGuest?: boolean; page?: number }) {
    const merged = { from, to, unpaidGuest, ...next };
    const query: Record<string, string> = {};
    if (merged.from) query.from = merged.from;
    if (merged.to) query.to = merged.to;
    if (merged.unpaidGuest) query.unpaidGuest = "true";
    if (next.page !== undefined && next.page > 1) query.page = String(next.page);
    setParams(query, { replace: next.page === undefined });
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">سفارش‌های بوفه</h2>
        <Button asChild size="sm">
          <Link to={paths.cafe}>بازگشت به بوفه</Link>
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>
            سفارش‌ها
            {orders.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(orders.data.totalCount)})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid max-w-xl gap-3 sm:grid-cols-2">
            <JalaliCalendarField
              label="از تاریخ"
              value={from ?? ""}
              onChange={(iso) => setFilter({ from: iso })}
            />
            <JalaliCalendarField
              label="تا تاریخ"
              value={to ?? ""}
              error={rangeIsValid ? undefined : errorMessages["CafeOrders.InvalidDateRange"]}
              onChange={(iso) => setFilter({ to: iso })}
            />
          </div>
          <label className="flex w-fit cursor-pointer items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="size-4 accent-primary"
              checked={unpaidGuest}
              onChange={(event) => setFilter({ unpaidGuest: event.target.checked })}
            />
            فقط پرداخت‌نشده — مهمان
          </label>

          {notice !== null && (
            <Alert variant="success" role="status">
              {notice}
            </Alert>
          )}

          {rangeIsValid && (
            <>
              {orders.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
              {orders.isError && <Alert variant="destructive">{errorMessage(orders.error)}</Alert>}
              {orders.isSuccess && orders.data.items.length === 0 && (
                <p className="text-muted-foreground">
                  {unpaidGuest
                    ? "سفارش پرداخت‌نشده‌ای از مهمان‌ها نمانده است."
                    : from === undefined && to === undefined
                      ? "هنوز سفارشی ثبت نشده است."
                      : "در این بازه سفارشی ثبت نشده است."}
                </p>
              )}
              {orders.isSuccess && orders.data.items.length > 0 && (
                <>
                  <CafeOrdersTable orders={orders.data.items} onDone={setNotice} />
                  <Pager
                    page={page}
                    pageCount={orders.data.pageCount}
                    onPageChange={(next) => setFilter({ page: next })}
                  />
                </>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
