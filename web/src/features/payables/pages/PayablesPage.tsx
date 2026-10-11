import { CalendarClock, Hourglass, Plus, ScrollText } from "lucide-react";
import { useState } from "react";
import { useSearchParams } from "react-router";

import { PageHero } from "@/components/PageHero";
import { EmptyState, LoadingState } from "@/components/States";
import { Pager } from "@/components/Pager";
import { StatTile } from "@/components/StatTile";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useExpenseCategories } from "@/features/expenses/api";
import { choiceClass, toneAction } from "@/lib/edges";
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
    <div className="space-y-6">
      {/* The banner like «پذیرش» and «فروش بوفه» (13.2i), «ثبت چک یا قسط» on it edged in the tone. */}
      <PageHero
        title="چک و قسط"
        actions={
          !adding && (
            <Button
              variant="ghost"
              className={toneAction}
              onClick={() => {
                setNotice(null);
                setAdding(true);
              }}
            >
              <Plus aria-hidden />
              ثبت چک یا قسط
            </Button>
          )
        }
      />

      {adding && (
        <Card edge>
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

      <Card edge>
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
          {/* The status reads like tabs, the chosen one like the active menu item (start edge); the
              kind is a filter's choice, edged at its end like a filter box (13.2i). */}
          <div className="flex flex-wrap gap-3">
            <div role="group" aria-label="وضعیت" className="flex flex-wrap gap-1">
              {[...payableStatuses, "all" as const].map((option) => (
                <Button
                  key={option}
                  type="button"
                  size="sm"
                  aria-pressed={tab === option}
                  variant="ghost"
                  className={choiceClass(tab === option)}
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
                  variant="ghost"
                  className={choiceClass(kind === option, "end")}
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

          {payables.isPending && <LoadingState />}
          {payables.isError && <Alert variant="destructive">{errorMessage(payables.error)}</Alert>}
          {payables.isSuccess && (
            <div className="space-y-2">
              <dl className="grid gap-3 sm:grid-cols-3">
                <StatTile
                  label="جمع در انتظار"
                  valueLabel="جمع در انتظار"
                  value={formatMoney(payables.data.pendingTotal)}
                  icon={Hourglass}
                  primary
                />
                <StatTile
                  label="چک‌ها"
                  valueLabel="جمع چک‌های در انتظار"
                  value={formatMoney(payables.data.pendingChequeTotal)}
                  icon={ScrollText}
                />
                <StatTile
                  label="قسط‌ها"
                  valueLabel="جمع قسط‌های در انتظار"
                  value={formatMoney(payables.data.pendingInstallmentTotal)}
                  icon={CalendarClock}
                />
              </dl>
              <p className="text-xs text-muted-foreground">
                آنچه باشگاه هنوز باید بپردازد. چک و قسط هزینه است: بعد از پاس شدن یا پرداخت، سیستم
                خودش آن را در هزینه‌ها ثبت می‌کند.
              </p>
            </div>
          )}
          {payables.isSuccess && payables.data.items.length === 0 && (
            <EmptyState>{emptyMessages[tab]}</EmptyState>
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
