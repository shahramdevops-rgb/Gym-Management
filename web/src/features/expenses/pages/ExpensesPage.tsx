import { useState } from "react";
import { useSearchParams } from "react-router";

import { JalaliCalendarField, SelectField } from "@/components/FormField";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage, errorMessages } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { dateFromParams, pageFromParams } from "@/lib/searchParams";

import { useExpenseCategories, useExpenseList, useRecordExpense } from "../api";
import { ExpenseCategoriesCard } from "../components/ExpenseCategoriesCard";
import { ExpenseForm } from "../components/ExpenseForm";
import { ExpensesTable } from "../components/ExpensesTable";
import { emptyExpenseValues } from "../schemas";

interface Filter {
  from?: string;
  to?: string;
  category?: string;
  page?: number;
}

/**
 * What the gym paid out (BUSINESS_RULES.md §9). Owner only, reading included: the route is
 * behind RequireRole and the API refuses staff anyway.
 *
 * The filter lives in the URL (`/expenses?from=2026-09-01&to=2026-09-30&category=…&page=2`), like
 * the cafe's order history, so a refresh or the back button returns to the same list. The range
 * is inclusive at both ends and either end may be left open. The total above the table is the
 * API's, over every page the filter matches, with voided expenses left out; the rows show them,
 * marked, because the list is the record of what was entered.
 */
export function ExpensesPage() {
  const [params, setParams] = useSearchParams();
  const from = dateFromParams(params, "from");
  const to = dateFromParams(params, "to");
  const categoryId = params.get("category") || undefined;
  const page = pageFromParams(params);
  const [notice, setNotice] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);

  const categories = useExpenseCategories();
  const recordExpense = useRecordExpense();

  const rangeIsValid = from === undefined || to === undefined || from <= to;
  // A backwards range is said beside the box rather than sent for the API to refuse.
  const expenses = useExpenseList({ from, to, categoryId, page }, { enabled: rangeIsValid });

  function setFilter(next: Filter) {
    const merged = { from, to, category: categoryId, ...next };
    const query: Record<string, string> = {};
    if (merged.from) query.from = merged.from;
    if (merged.to) query.to = merged.to;
    if (merged.category) query.category = merged.category;
    if (next.page !== undefined && next.page > 1) query.page = String(next.page);
    setParams(query, { replace: next.page === undefined });
  }

  const filtered = from !== undefined || to !== undefined || categoryId !== undefined;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">هزینه‌ها</h2>
        {!adding && (
          <Button
            size="sm"
            disabled={!categories.isSuccess}
            onClick={() => {
              setNotice(null);
              setAdding(true);
            }}
          >
            ثبت هزینه
          </Button>
        )}
      </div>

      {adding && categories.isSuccess && (
        <Card>
          <CardHeader>
            <CardTitle>هزینهٔ جدید</CardTitle>
          </CardHeader>
          <CardContent>
            <ExpenseForm
              defaultValues={emptyExpenseValues()}
              categories={categories.data}
              submitLabel="ثبت هزینه"
              onSubmit={async (input) => {
                await recordExpense.mutateAsync(input);
                setAdding(false);
                setNotice(`هزینه به مبلغ ${formatMoney(input.amount)} ثبت شد.`);
              }}
              onCancel={() => setAdding(false)}
            />
          </CardContent>
        </Card>
      )}

      <div className="grid items-start gap-4 xl:grid-cols-[1fr_20rem]">
        <Card>
          <CardHeader>
            <CardTitle>
              فهرست هزینه‌ها
              {expenses.isSuccess && (
                <span className="ms-2 text-sm font-normal text-muted-foreground">
                  ({toPersianDigits(expenses.data.totalCount)})
                </span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <JalaliCalendarField
                label="از تاریخ"
                value={from ?? ""}
                onChange={(iso) => setFilter({ from: iso })}
              />
              <JalaliCalendarField
                label="تا تاریخ"
                value={to ?? ""}
                error={rangeIsValid ? undefined : errorMessages["Expenses.InvalidDateRange"]}
                onChange={(iso) => setFilter({ to: iso })}
              />
              <SelectField
                label="دسته‌بندی"
                value={categoryId ?? ""}
                onChange={(event) => setFilter({ category: event.target.value })}
              >
                <option value="">همه</option>
                {categories.data?.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </SelectField>
            </div>

            {notice !== null && (
              <Alert variant="success" role="status">
                {notice}
              </Alert>
            )}

            {rangeIsValid && (
              <>
                {expenses.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
                {expenses.isError && (
                  <Alert variant="destructive">{errorMessage(expenses.error)}</Alert>
                )}
                {expenses.isSuccess && (
                  <div className="rounded-md border bg-muted/30 p-3">
                    <p className="text-sm text-muted-foreground">
                      {filtered ? "جمع هزینه‌ها در این فیلتر" : "جمع همهٔ هزینه‌ها"}
                    </p>
                    <p className="text-lg font-bold" aria-label="جمع هزینه‌ها">
                      {formatMoney(expenses.data.totalAmount)}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      هزینه‌های باطل‌شده در این جمع حساب نمی‌شوند.
                    </p>
                  </div>
                )}
                {expenses.isSuccess && expenses.data.items.length === 0 && (
                  <p className="text-muted-foreground">
                    {filtered
                      ? "در این فیلتر هزینه‌ای ثبت نشده است."
                      : "هنوز هزینه‌ای ثبت نشده است."}
                  </p>
                )}
                {expenses.isSuccess && expenses.data.items.length > 0 && (
                  <>
                    <ExpensesTable
                      expenses={expenses.data.items}
                      categories={categories.data ?? []}
                      onDone={setNotice}
                    />
                    <Pager
                      page={page}
                      pageCount={expenses.data.pageCount}
                      onPageChange={(next) => setFilter({ page: next })}
                    />
                  </>
                )}
              </>
            )}
          </CardContent>
        </Card>

        <ExpenseCategoriesCard />
      </div>
    </div>
  );
}
