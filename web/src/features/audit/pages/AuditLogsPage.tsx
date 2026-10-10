import { X } from "lucide-react";
import { useMemo } from "react";
import { useSearchParams } from "react-router";

import { JalaliCalendarField, SelectField } from "@/components/FormField";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { MemberFilter } from "@/features/history/components/MemberFilter";
import { errorMessage, errorMessages } from "@/lib/errors";
import { gymToday, toPersianDigits } from "@/lib/format";
import { dateFromParams, pageFromParams } from "@/lib/searchParams";

import { useAuditLogs, useAuditUsers, type AuditAction } from "../api";
import { AuditLogTable } from "../components/AuditLogTable";
import { auditActionLabels, auditEntityTypes, entityLabel, signInEntityTypes } from "../labels";

/** The value of the "who" box for the rows no user did. */
const systemUser = "system";

interface Filter {
  from?: string;
  to?: string;
  user?: string;
  type?: string;
  id?: string;
  action?: string;
  member?: string;
  signIns?: boolean;
  page?: number;
}

function actionOf(value: string | null): AuditAction | undefined {
  return value !== null && value in auditActionLabels ? (value as AuditAction) : undefined;
}

/**
 * «گزارش تغییرات», the audit screen (BUSINESS_RULES.md §11 *The audit screen*). Owner only: the
 * route is behind RequireRole and the API refuses staff anyway.
 *
 * The filter lives in the URL (`/audit-logs?from=…&user=system&type=Payment&member=…&page=2`).
 * With no dates, member or record in it the page opens on today; choosing a member or a record
 * shows their whole history instead.
 */
export function AuditLogsPage() {
  const [params, setParams] = useSearchParams();
  const chosenFrom = dateFromParams(params, "from");
  const to = dateFromParams(params, "to");
  const user = params.get("user") ?? undefined;
  const type = params.get("type") ?? undefined;
  const id = type === undefined ? undefined : (params.get("id") ?? undefined);
  const action = actionOf(params.get("action"));
  const member = params.get("member") ?? undefined;
  const signIns = params.get("signIns") === "1";
  const page = pageFromParams(params);

  const today =
    chosenFrom === undefined && to === undefined && member === undefined && id === undefined;
  const from = today ? gymToday() : chosenFrom;

  const rangeIsValid = from === undefined || to === undefined || from <= to;
  // A backwards range is said beside the box rather than sent for the API to refuse.
  const logs = useAuditLogs(
    {
      from,
      to,
      userId: user === systemUser ? undefined : user,
      systemOnly: user === systemUser,
      entityType: type,
      entityId: id,
      action,
      memberId: member,
      includeSignIns: signIns,
      page,
    },
    { enabled: rangeIsValid },
  );
  const users = useAuditUsers();
  const userNames = useMemo(
    () => new Map((users.data ?? []).map((account) => [account.id, account.fullName])),
    [users.data],
  );

  function setFilter(next: Filter) {
    // Today's date goes into the URL only once a date box is touched; a member or a record chosen
    // from a row leaves the dates as they were, so "today" gives way to their whole history.
    const touchesDates = "from" in next || "to" in next;
    const merged: Filter = {
      from: touchesDates ? from : chosenFrom,
      to,
      user,
      type,
      id,
      action,
      member,
      signIns,
      ...next,
    };
    const query: Record<string, string> = {};
    if (merged.from) query.from = merged.from;
    if (merged.to) query.to = merged.to;
    if (merged.user) query.user = merged.user;
    if (merged.type) query.type = merged.type;
    if (merged.type && merged.id) query.id = merged.id;
    if (merged.action) query.action = merged.action;
    if (merged.member) query.member = merged.member;
    if (merged.signIns) query.signIns = "1";
    if (next.page !== undefined && next.page > 1) query.page = String(next.page);
    setParams(query, { replace: next.page === undefined });
  }

  // The sign-in kinds are offered only while they are shown, unless one is already chosen.
  const typeOptions = auditEntityTypes.filter(
    (option) => signIns || option.type === type || !signInEntityTypes.includes(option.type),
  );

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-bold">گزارش تغییرات</h2>

      <Card>
        <CardHeader>
          <CardTitle>
            {today ? "تغییرهای امروز" : "تغییرها"}
            {logs.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(logs.data.totalCount)})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <JalaliCalendarField
              label="از تاریخ"
              value={from ?? ""}
              onChange={(iso) => setFilter({ from: iso })}
            />
            <JalaliCalendarField
              label="تا تاریخ"
              value={to ?? ""}
              error={rangeIsValid ? undefined : errorMessages["Audit.InvalidDateRange"]}
              onChange={(iso) => setFilter({ to: iso })}
            />
            <SelectField
              label="کاربر"
              value={user ?? ""}
              onChange={(event) => setFilter({ user: event.target.value })}
            >
              <option value="">همه</option>
              <option value={systemUser}>سیستم</option>
              {(users.data ?? []).map((account) => (
                <option key={account.id} value={account.id}>
                  {account.fullName}
                </option>
              ))}
            </SelectField>
            <SelectField
              label="نوع رکورد"
              value={type ?? ""}
              onChange={(event) => setFilter({ type: event.target.value, id: undefined })}
            >
              <option value="">همه</option>
              {typeOptions.map((option) => (
                <option key={option.type} value={option.type}>
                  {option.label}
                </option>
              ))}
            </SelectField>
            <SelectField
              label="نوع تغییر"
              value={action ?? ""}
              onChange={(event) => setFilter({ action: event.target.value })}
            >
              <option value="">همه</option>
              {Object.entries(auditActionLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </SelectField>
            <MemberFilter
              memberId={member}
              onChange={(memberId) => setFilter({ member: memberId })}
            />
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="size-4"
              checked={signIns}
              onChange={(event) => setFilter({ signIns: event.target.checked })}
            />
            نمایش ورودها و دستگاه‌ها
          </label>

          {type !== undefined && id !== undefined && (
            <div className="flex flex-wrap items-center gap-3 rounded-md border bg-muted/30 px-3 py-2 text-sm">
              <span>
                فقط تغییرهای یک {entityLabel(type)}، با شناسهٔ{" "}
                <span dir="ltr" className="font-mono">
                  {id.slice(0, 8)}
                </span>
              </span>
              <Button size="sm" variant="ghost" onClick={() => setFilter({ id: undefined })}>
                <X aria-hidden />
                همهٔ رکوردها
              </Button>
            </div>
          )}

          {rangeIsValid && (
            <>
              {logs.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
              {logs.isError && <Alert variant="destructive">{errorMessage(logs.error)}</Alert>}
              {logs.isSuccess && logs.data.items.length === 0 && (
                <p className="text-muted-foreground">تغییری با این فیلتر نیست.</p>
              )}
              {logs.isSuccess && logs.data.items.length > 0 && (
                <>
                  <AuditLogTable
                    rows={logs.data.items}
                    userNames={userNames}
                    onMember={(memberId) => setFilter({ member: memberId })}
                    onRecord={(entityType, entityId) =>
                      setFilter({ type: entityType, id: entityId })
                    }
                  />
                  <Pager
                    page={page}
                    pageCount={logs.data.pageCount}
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
