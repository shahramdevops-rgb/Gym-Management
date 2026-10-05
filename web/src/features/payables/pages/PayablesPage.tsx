import { useState } from "react";
import { useSearchParams } from "react-router";

import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useExpenseCategories } from "@/features/expenses/api";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { pageFromParams } from "@/lib/searchParams";

import {
  payableKindLabels,
  payableKinds,
  payableStatuses,
  payableStatusLabels,
  usePayableList,
  useRegisterPayable,
  type PayableKind,
  type PayableStatus,
} from "../api";
import { PayableForm } from "../components/PayableForm";
import { PayablesTable } from "../components/PayablesTable";
import { emptyPayableValues } from "../schemas";

/** The status tab in the URL: none is «در انتظار», the question the page is opened for. */
type Tab = PayableStatus | "all";

/** The kind filter in the URL: none is both. */
type KindFilter = PayableKind | "all";

function tabFromParams(params: URLSearchParams): Tab {
  const value = params.get("status");
  if (value === "all") {
    return "all";
  }
  return payableStatuses.find((status) => status === value) ?? "Pending";
}

function kindFromParams(params: URLSearchParams): KindFilter {
  const value = params.get("kind");
  return payableKinds.find((kind) => kind === value) ?? "all";
}

const emptyMessages: Record<Tab, string> = {
  Pending: "چک یا قسطِ در انتظاری نیست.",
  Paid: "هنوز چک یا قسطی پرداخت نشده است.",
  Cancelled: "مورد باطل‌شده‌ای نیست.",
  all: "هنوز چک یا قسطی ثبت نشده است.",
};

/**
 * The cheques the gym gave and its instalments, «چک و قسط» (BUSINESS_RULES.md §9 *Cheques and
 * instalments*). Owner only, reading included: the route is behind RequireRole and the API refuses
 * staff anyway.
 *
 * Paying one records its expense, so the page is where both are kept in step. The filters live in
 * the URL (`/payables?status=Paid&kind=Installment&page=2`), like the expenses' filter, so a
 * refresh or the back button returns to the same list. The totals are the API's, over everything
 * pending whatever the filter.
 */
export function PayablesPage() {
  const [params, setParams] = useSearchParams();
  const tab = tabFromParams(params);
  const kind = kindFromParams(params);
  const page = pageFromParams(params);
  const [notice, setNotice] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);

  const registerPayable = useRegisterPayable();
  const categories = useExpenseCategories();
  const payables = usePayableList({
    status: tab === "all" ? undefined : tab,
    kind: kind === "all" ? undefined : kind,
    page,
  });

  function setFilter(next: { tab?: Tab; kind?: KindFilter; page?: number }) {
    const nextTab = next.tab ?? tab;
    const nextKind = next.kind ?? kind;
    const nextPage = next.page ?? 1;
    const query: Record<string, string> = {};
    if (nextTab !== "Pending") query.status = nextTab;
    if (nextKind !== "all") query.kind = nextKind;
    if (nextPage > 1) query.page = String(nextPage);
    setParams(query, { replace: nextPage === 1 });
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">چک و قسط</h2>
        {!adding && (
          <Button
            size="sm"
            onClick={() => {
              setNotice(null);
              setAdding(true);
            }}
          >
            ثبت چک یا قسط
          </Button>
        )}
      </div>

      {adding && (
        <Card>
          <CardHeader>
            <CardTitle>چک یا قسط جدید</CardTitle>
          </CardHeader>
          <CardContent>
            <PayableForm
              defaultValues={emptyPayableValues()}
              categories={categories.data ?? []}
              submitLabel="ثبت"
              onSubmit={async (input) => {
                await registerPayable.mutateAsync(input);
                setAdding(false);
                setNotice(
                  `${payableKindLabels[input.kind]} به مبلغ ${formatMoney(input.amount)} ثبت شد.`,
                );
              }}
              onCancel={() => setAdding(false)}
            />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>
            فهرست چک و قسط
            {payables.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(payables.data.totalCount)})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-3">
            <div role="group" aria-label="وضعیت" className="flex flex-wrap gap-1">
              {[...payableStatuses, "all" as const].map((option) => (
                <Button
                  key={option}
                  type="button"
                  size="sm"
                  aria-pressed={tab === option}
                  variant={tab === option ? "secondary" : "outline"}
                  onClick={() => setFilter({ tab: option })}
                >
                  {option === "all" ? "همه" : payableStatusLabels[option]}
                </Button>
              ))}
            </div>
            <div role="group" aria-label="نوع" className="flex flex-wrap gap-1">
              {(["all", ...payableKinds] as const).map((option) => (
                <Button
                  key={option}
                  type="button"
                  size="sm"
                  aria-pressed={kind === option}
                  variant={kind === option ? "secondary" : "outline"}
                  onClick={() => setFilter({ kind: option })}
                >
                  {option === "all" ? "چک و قسط" : payableKindLabels[option]}
                </Button>
              ))}
            </div>
          </div>

          {notice !== null && (
            <Alert variant="success" role="status">
              {notice}
            </Alert>
          )}

          {payables.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {payables.isError && <Alert variant="destructive">{errorMessage(payables.error)}</Alert>}
          {payables.isSuccess && (
            <div className="space-y-2 rounded-md border bg-muted/30 p-3">
              <div className="flex flex-wrap gap-x-8 gap-y-2">
                <div>
                  <p className="text-sm text-muted-foreground">جمع در انتظار</p>
                  <p className="text-lg font-bold" aria-label="جمع در انتظار">
                    {formatMoney(payables.data.pendingTotal)}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">چک‌ها</p>
                  <p className="font-medium" aria-label="جمع چک‌های در انتظار">
                    {formatMoney(payables.data.pendingChequeTotal)}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">قسط‌ها</p>
                  <p className="font-medium" aria-label="جمع قسط‌های در انتظار">
                    {formatMoney(payables.data.pendingInstallmentTotal)}
                  </p>
                </div>
              </div>
              <p className="text-xs text-muted-foreground">
                آنچه باشگاه هنوز باید بپردازد. چک و قسط هزینه است: بعد از پاس شدن یا پرداخت، سیستم
                خودش آن را در هزینه‌ها ثبت می‌کند.
              </p>
            </div>
          )}
          {payables.isSuccess && payables.data.items.length === 0 && (
            <p className="text-muted-foreground">{emptyMessages[tab]}</p>
          )}
          {payables.isSuccess && payables.data.items.length > 0 && (
            <>
              <PayablesTable
                payables={payables.data.items}
                categories={categories.data ?? []}
                onDone={setNotice}
              />
              <Pager
                page={page}
                pageCount={payables.data.pageCount}
                onPageChange={(next) => setFilter({ page: next })}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
