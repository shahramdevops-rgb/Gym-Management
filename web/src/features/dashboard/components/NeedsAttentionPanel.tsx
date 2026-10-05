import type { ReactNode } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatDate, formatMoney, formatNumber, formatPhone, toPersianDigits } from "@/lib/format";

import { reportThresholds, type NeedsAttention } from "../api";

const lowSessions = toPersianDigits(reportThresholds.lowSessions);
const expiringWithinDays = toPersianDigits(reportThresholds.expiringWithinDays);
const absentDays = toPersianDigits(reportThresholds.absentDays);
const windowDays = toPersianDigits(reportThresholds.windowDays);
const chequeDueWithinDays = toPersianDigits(reportThresholds.chequeDueWithinDays);

interface PersonRow {
  memberId: string;
  fullName: string;
  phoneNumber: string;
}

interface AttentionListProps<T extends PersonRow> {
  id: string;
  title: string;
  /** The rule the list follows, in one line, so the Owner knows why someone is on it. */
  rule: string;
  rows: T[];
  detail: (row: T) => ReactNode;
}

/** One list: who to call, with their mobile, and why. Each name opens the member's profile. */
function AttentionList<T extends PersonRow>({
  id,
  title,
  rule,
  rows,
  detail,
}: AttentionListProps<T>) {
  const headingId = `attention-${id}`;

  return (
    <section aria-labelledby={headingId} className="space-y-2 rounded-md border p-3">
      <div className="flex items-baseline justify-between gap-2">
        <h4 id={headingId} className="font-medium">
          {title}
        </h4>
        <span className="text-sm text-muted-foreground">{formatNumber(rows.length)} نفر</span>
      </div>
      <p className="text-xs text-muted-foreground">{rule}</p>
      {rows.length === 0 ? (
        <p className="text-sm text-muted-foreground">کسی در این فهرست نیست.</p>
      ) : (
        <ul className="max-h-72 divide-y overflow-y-auto">
          {rows.map((row) => (
            <li
              key={row.memberId}
              className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2 text-sm"
            >
              <div className="space-y-0.5">
                <Link to={paths.member(row.memberId)} className="font-medium hover:underline">
                  {row.fullName}
                </Link>
                <p className="text-xs text-muted-foreground">
                  <span dir="ltr">{formatPhone(row.phoneNumber)}</span>
                </p>
              </div>
              <div className="text-end text-xs">{detail(row)}</div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/**
 * The gym's own cheques coming due (BUSINESS_RULES.md §9 *Cheques*): not people to call, so not an
 * AttentionList. A cheque past its date stays here until it is marked «پاس شد» on the cheques page.
 */
function ChequesDueList({ rows, today }: { rows: NeedsAttention["chequesDue"]; today: string }) {
  return (
    <section aria-labelledby="attention-cheques" className="space-y-2 rounded-md border p-3">
      <div className="flex items-baseline justify-between gap-2">
        <h4 id="attention-cheques" className="font-medium">
          چک‌های نزدیک سررسید
        </h4>
        <span className="text-sm text-muted-foreground">{formatNumber(rows.length)} چک</span>
      </div>
      <p className="text-xs text-muted-foreground">
        {`چک‌های در انتظار تا ${chequeDueWithinDays} روز دیگر، و چک‌هایی که تاریخشان گذشته و هنوز پاس نشده‌اند.`}
      </p>
      {rows.length === 0 ? (
        <p className="text-sm text-muted-foreground">چکی در این فهرست نیست.</p>
      ) : (
        <ul className="max-h-72 divide-y overflow-y-auto">
          {rows.map((row) => {
            const overdue = row.dueDate < today;
            return (
              <li
                key={row.chequeId}
                className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2 text-sm"
              >
                <div className="space-y-0.5">
                  <p className="font-medium">{row.payee}</p>
                  <p className="text-xs text-muted-foreground">{row.description}</p>
                </div>
                <div className="text-end text-xs">
                  <p className="font-medium">{formatMoney(row.amount)}</p>
                  <p className={overdue ? "font-medium text-destructive" : "text-muted-foreground"}>
                    {overdue ? "سررسید گذشته، " : row.dueDate === today ? "امروز، " : ""}
                    {formatDate(row.dueDate)}
                  </p>
                </div>
              </li>
            );
          })}
        </ul>
      )}
      <Link to={paths.cheques} className="inline-block text-sm font-medium hover:underline">
        همهٔ چک‌ها
      </Link>
    </section>
  );
}

/**
 * Who the Owner should call today (BUSINESS_RULES.md §12 *Needs attention*), whatever range the
 * dashboard shows. One member can be on more than one list: each list is its own question.
 */
export function NeedsAttentionPanel({ data }: { data: NeedsAttention }) {
  const withoutMember = Number(data.oldDebtWithoutMember);

  return (
    <Card>
      <CardHeader>
        <CardTitle>نیاز به اقدام</CardTitle>
        <CardDescription>
          کسانی که امروز باید با آن‌ها تماس گرفت، و چک‌هایی که باید پرداخت شوند.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3 lg:grid-cols-2">
        <AttentionList
          id="running-out"
          title="رو به پایان، بدون تمدید"
          rule={`${lowSessions} جلسه یا کمتر مانده، یا تا ${expiringWithinDays} روز دیگر تمام می‌شود، و پلن بعدی را نخریده است.`}
          rows={data.runningOut}
          detail={(row) => {
            const left = Number(row.sessionsLeft);
            return (
              <>
                <p>{left === 0 ? "جلسه‌ای نمانده" : `${formatNumber(left)} جلسه مانده`}</p>
                <p className="text-muted-foreground">پایان {formatDate(row.endDate)}</p>
              </>
            );
          }}
        />
        <AttentionList
          id="left"
          title={`رفته در ${windowDays} روز اخیر`}
          rule={`آخرین پلنش در ${windowDays} روز گذشته تمام شده و پلن دیگری ندارد.`}
          rows={data.left}
          detail={(row) => <p className="text-muted-foreground">پایان {formatDate(row.endedOn)}</p>}
        />
        <AttentionList
          id="absent"
          title="مدتی است نیامده"
          rule={`پلن قابل استفاده دارد و ${absentDays} روز یا بیشتر است که نیامده.`}
          rows={data.absent}
          detail={(row) => (
            <>
              <p>{formatNumber(Number(row.daysAway))} روز</p>
              <p className="text-muted-foreground">
                {row.lastVisitOn === null
                  ? "از شروع پلن نیامده"
                  : `آخرین ورود ${formatDate(row.lastVisitOn)}`}
              </p>
            </>
          )}
        />
        <div className="space-y-2">
          <AttentionList
            id="old-debts"
            title="بدهی قدیمی"
            rule={`بدهی روی فروش‌هایی که بیش از ${windowDays} روز از ثبتشان گذشته است.`}
            rows={data.oldDebts}
            detail={(row) => (
              <>
                <p className="font-medium">{formatMoney(row.owed)}</p>
                <p className="text-muted-foreground">از {formatDate(row.oldestSaleOn)}</p>
              </>
            )}
          />
          {withoutMember > 0 && (
            <p className="text-sm text-muted-foreground">
              بدهی قدیمی مشتری آزاد بوفه و مهمان‌ها: {formatMoney(data.oldDebtWithoutMember)}
            </p>
          )}
        </div>
        <ChequesDueList rows={data.chequesDue} today={data.today} />
      </CardContent>
    </Card>
  );
}
