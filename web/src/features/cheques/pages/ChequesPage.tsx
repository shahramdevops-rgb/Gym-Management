import { useState } from "react";
import { useSearchParams } from "react-router";

import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { pageFromParams } from "@/lib/searchParams";

import {
  chequeStatuses,
  chequeStatusLabels,
  useChequeList,
  useRegisterCheque,
  type ChequeStatus,
} from "../api";
import { ChequeForm } from "../components/ChequeForm";
import { ChequesTable } from "../components/ChequesTable";
import { emptyChequeValues } from "../schemas";

/** The tab in the URL: none is «در انتظار», the question the page is opened for; `all` is every cheque. */
type Tab = ChequeStatus | "all";

function tabFromParams(params: URLSearchParams): Tab {
  const value = params.get("status");
  if (value === "all") {
    return "all";
  }
  return chequeStatuses.find((status) => status === value) ?? "Pending";
}

const emptyMessages: Record<Tab, string> = {
  Pending: "چک در انتظاری نیست.",
  Passed: "هنوز چکی پاس نشده است.",
  Cancelled: "چک باطل‌شده‌ای نیست.",
  all: "هنوز چکی ثبت نشده است.",
};

/**
 * The cheques the gym gave (BUSINESS_RULES.md §9 *Cheques*). Owner only, reading included: the
 * route is behind RequireRole and the API refuses staff anyway.
 *
 * A cheque is not an expense: the page only keeps the register and the reminder. The tab lives in
 * the URL (`/cheques?status=Passed&page=2`), like the expenses' filter, so a refresh or the back
 * button returns to the same list. The total is the API's, over every pending cheque.
 */
export function ChequesPage() {
  const [params, setParams] = useSearchParams();
  const tab = tabFromParams(params);
  const page = pageFromParams(params);
  const [notice, setNotice] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);

  const registerCheque = useRegisterCheque();
  const cheques = useChequeList({ status: tab === "all" ? undefined : tab, page });

  function setTab(next: Tab, nextPage = 1) {
    const query: Record<string, string> = {};
    if (next !== "Pending") query.status = next;
    if (nextPage > 1) query.page = String(nextPage);
    setParams(query, { replace: nextPage === 1 });
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">چک‌ها</h2>
        {!adding && (
          <Button
            size="sm"
            onClick={() => {
              setNotice(null);
              setAdding(true);
            }}
          >
            ثبت چک
          </Button>
        )}
      </div>

      {adding && (
        <Card>
          <CardHeader>
            <CardTitle>چک جدید</CardTitle>
          </CardHeader>
          <CardContent>
            <ChequeForm
              defaultValues={emptyChequeValues()}
              submitLabel="ثبت چک"
              onSubmit={async (input) => {
                await registerCheque.mutateAsync(input);
                setAdding(false);
                setNotice(`چک به مبلغ ${formatMoney(input.amount)} ثبت شد.`);
              }}
              onCancel={() => setAdding(false)}
            />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>
            فهرست چک‌ها
            {cheques.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(cheques.data.totalCount)})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div role="group" aria-label="وضعیت چک" className="flex flex-wrap gap-1">
            {[...chequeStatuses, "all" as const].map((option) => (
              <Button
                key={option}
                type="button"
                size="sm"
                aria-pressed={tab === option}
                variant={tab === option ? "secondary" : "outline"}
                onClick={() => setTab(option)}
              >
                {option === "all" ? "همه" : chequeStatusLabels[option]}
              </Button>
            ))}
          </div>

          {notice !== null && (
            <Alert variant="success" role="status">
              {notice}
            </Alert>
          )}

          {cheques.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {cheques.isError && <Alert variant="destructive">{errorMessage(cheques.error)}</Alert>}
          {cheques.isSuccess && (
            <div className="rounded-md border bg-muted/30 p-3">
              <p className="text-sm text-muted-foreground">جمع چک‌های در انتظار</p>
              <p className="text-lg font-bold" aria-label="جمع چک‌های در انتظار">
                {formatMoney(cheques.data.pendingTotal)}
              </p>
              <p className="text-xs text-muted-foreground">
                آنچه باشگاه هنوز باید بپردازد. چک هزینه نیست: روز پاس شدن، هزینه را خودتان ثبت کنید.
              </p>
            </div>
          )}
          {cheques.isSuccess && cheques.data.items.length === 0 && (
            <p className="text-muted-foreground">{emptyMessages[tab]}</p>
          )}
          {cheques.isSuccess && cheques.data.items.length > 0 && (
            <>
              <ChequesTable cheques={cheques.data.items} onDone={setNotice} />
              <Pager
                page={page}
                pageCount={cheques.data.pageCount}
                onPageChange={(next) => setTab(tab, next)}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
