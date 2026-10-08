import { useState } from "react";
import { useSearchParams } from "react-router";

import { JalaliCalendarField, SelectField } from "@/components/FormField";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage, errorMessages } from "@/lib/errors";
import {
  formatMoney,
  gymToday,
  jalaliMonthNames,
  jalaliPartsOf,
  jalaliToIso,
  toPersianDigits,
} from "@/lib/format";
import { dateFromParams, pageFromParams } from "@/lib/searchParams";

import { useSmsMessages, type SmsMessageKind, type SmsMessageStatus } from "../api";
import { SmsCreditLine } from "../components/SmsCreditLine";
import { SmsMessagesTable } from "../components/SmsMessagesTable";
import { smsKindLabels, smsStatusLabels } from "../labels";

interface Filter {
  from?: string;
  to?: string;
  kind?: string;
  status?: string;
  page?: number;
}

/** The first day of this Jalali month, and its name: what the page opens on. */
function currentMonth(): { from: string; name: string } {
  const today = jalaliPartsOf(gymToday())!;

  return {
    from: jalaliToIso(today.year, today.month, 1)!,
    name: `${jalaliMonthNames[today.month - 1]} ${toPersianDigits(today.year)}`,
  };
}

function oneOf<T extends string>(value: string | null, allowed: Record<T, string>): T | undefined {
  return value !== null && value in allowed ? (value as T) : undefined;
}

/**
 * The SMS history, پیامک‌ها (BUSINESS_RULES.md §10 *The SMS history*). Owner only: the route is
 * behind RequireRole and the API refuses staff anyway.
 *
 * The filter lives in the URL (`/sms?from=2026-09-23&kind=Birthday&status=Failed&page=2`), like
 * the expenses. With no dates in it the page opens on the current Jalali month, so the total above
 * the table is the month's cost; an earlier "from" reaches further back.
 */
export function SmsMessagesPage() {
  const [params, setParams] = useSearchParams();
  const month = currentMonth();
  const chosenFrom = dateFromParams(params, "from");
  const to = dateFromParams(params, "to");
  const thisMonth = chosenFrom === undefined && to === undefined;
  const from = thisMonth ? month.from : chosenFrom;
  const kind = oneOf<SmsMessageKind>(params.get("kind"), smsKindLabels);
  const status = oneOf<SmsMessageStatus>(params.get("status"), smsStatusLabels);
  const page = pageFromParams(params);
  const [notice, setNotice] = useState<string | null>(null);

  const rangeIsValid = from === undefined || to === undefined || from <= to;
  // A backwards range is said beside the box rather than sent for the API to refuse.
  const messages = useSmsMessages({ from, to, kind, status, page }, { enabled: rangeIsValid });

  function setFilter(next: Filter) {
    // The month's first day goes into the URL only once a date box is touched: a kind, a status or
    // a page keeps the page on "this month".
    const touchesDates = "from" in next || "to" in next;
    const merged = { from: touchesDates ? from : chosenFrom, to, kind, status, ...next };
    const query: Record<string, string> = {};
    if (merged.from) query.from = merged.from;
    if (merged.to) query.to = merged.to;
    if (merged.kind) query.kind = merged.kind;
    if (merged.status) query.status = merged.status;
    if (next.page !== undefined && next.page > 1) query.page = String(next.page);
    setParams(query, { replace: next.page === undefined });
  }

  const filtered = kind !== undefined || status !== undefined;

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-bold">پیامک‌ها</h2>

      <SmsCreditLine />

      <Card>
        <CardHeader>
          <CardTitle>
            پیامک‌های فرستاده‌شده
            {messages.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(messages.data.totalCount)})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <JalaliCalendarField
              label="از تاریخ"
              value={from ?? ""}
              onChange={(iso) => setFilter({ from: iso })}
            />
            <JalaliCalendarField
              label="تا تاریخ"
              value={to ?? ""}
              error={rangeIsValid ? undefined : errorMessages["Notifications.InvalidDateRange"]}
              onChange={(iso) => setFilter({ to: iso })}
            />
            <SelectField
              label="نوع"
              value={kind ?? ""}
              onChange={(event) => setFilter({ kind: event.target.value })}
            >
              <option value="">همه</option>
              {Object.entries(smsKindLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </SelectField>
            <SelectField
              label="وضعیت"
              value={status ?? ""}
              onChange={(event) => setFilter({ status: event.target.value })}
            >
              <option value="">همه</option>
              {Object.entries(smsStatusLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
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
              {messages.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
              {messages.isError && (
                <Alert variant="destructive">{errorMessage(messages.error)}</Alert>
              )}
              {messages.isSuccess && (
                <div className="rounded-md border bg-muted/30 p-3">
                  <p className="text-sm text-muted-foreground">
                    {thisMonth && !filtered
                      ? `هزینهٔ پیامک‌های ${month.name}`
                      : "هزینهٔ پیامک‌های این فیلتر"}
                  </p>
                  <p className="text-lg font-bold" aria-label="هزینهٔ پیامک‌ها">
                    {formatMoney(messages.data.totalCostToman)}
                  </p>
                </div>
              )}
              {messages.isSuccess && messages.data.items.length === 0 && (
                <p className="text-muted-foreground">در این بازه پیامکی نیست.</p>
              )}
              {messages.isSuccess && messages.data.items.length > 0 && (
                <>
                  <SmsMessagesTable messages={messages.data.items} onDone={setNotice} />
                  <Pager
                    page={page}
                    pageCount={messages.data.pageCount}
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
