import {
  ArrowLeft,
  BellRing,
  CalendarClock,
  Coins,
  Hourglass,
  UserMinus,
  UserX,
  type LucideIcon,
} from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { kindText } from "@/features/payables/api";
import { formatDate, formatMoney, formatNumber, formatPhone, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import { reportThresholds, type AbsenceSmsEffect, type NeedsAttention } from "../api";
import { toneStyle, type Tone } from "@/lib/tone";
import { AbsenceSmsOverallBox } from "./AbsenceSmsOverallBox";
import { Initial } from "./Initial";
import { ListFrame } from "./ListFrame";

const lowSessions = toPersianDigits(reportThresholds.lowSessions);
const expiringWithinDays = toPersianDigits(reportThresholds.expiringWithinDays);
const absentDays = toPersianDigits(reportThresholds.absentDays);
const windowDays = toPersianDigits(reportThresholds.windowDays);
const payableDueWithinDays = toPersianDigits(reportThresholds.payableDueWithinDays);

interface PersonRow {
  memberId: string;
  fullName: string;
  phoneNumber: string;
}

interface AttentionListProps<T extends PersonRow> {
  id: string;
  title: string;
  icon: LucideIcon;
  tone: Tone;
  rule: string;
  rows: T[];
  detail: (row: T) => ReactNode;
}

/** One list: who to call, with their mobile, and why. Each name opens the member's profile. */
function AttentionList<T extends PersonRow>({
  id,
  title,
  icon,
  tone,
  rule,
  rows,
  detail,
}: AttentionListProps<T>) {
  return (
    <ListFrame
      headingId={`attention-${id}`}
      title={title}
      icon={icon}
      tone={tone}
      count={`${formatNumber(rows.length)} نفر`}
      rule={rule}
    >
      {rows.length === 0 ? (
        <p className="py-2 text-sm text-muted-foreground">کسی در این فهرست نیست.</p>
      ) : (
        <ul className="max-h-72 divide-y overflow-y-auto">
          {rows.map((row) => (
            <li
              key={row.memberId}
              className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2 text-sm"
            >
              <div className="flex items-center gap-2.5">
                <Initial name={row.fullName} tone={tone} />
                <div className="space-y-0.5">
                  <Link to={paths.member(row.memberId)} className="font-medium hover:underline">
                    {row.fullName}
                  </Link>
                  <p className="text-xs text-muted-foreground">
                    <span dir="ltr">{formatPhone(row.phoneNumber)}</span>
                  </p>
                </div>
              </div>
              <div className="text-end text-xs">{detail(row)}</div>
            </li>
          ))}
        </ul>
      )}
    </ListFrame>
  );
}

/**
 * The gym's own cheques and instalments coming due (BUSINESS_RULES.md §9 *Cheques and instalments*):
 * not people to call, so not an AttentionList. One past its date stays here until it is marked paid
 * on the «چک و قسط» page.
 */
function PayablesDueList({ rows, today }: { rows: NeedsAttention["payablesDue"]; today: string }) {
  return (
    <ListFrame
      headingId="attention-payables"
      title="چک و قسط نزدیک سررسید"
      icon={CalendarClock}
      tone="violet"
      count={`${formatNumber(rows.length)} مورد`}
      rule={`چک و قسطِ در انتظار تا ${payableDueWithinDays} روز دیگر، و آن‌هایی که تاریخشان گذشته و هنوز پرداخت نشده‌اند.`}
    >
      {rows.length === 0 ? (
        <p className="py-2 text-sm text-muted-foreground">چک یا قسطی در این فهرست نیست.</p>
      ) : (
        <ul className="max-h-72 space-y-1 overflow-y-auto">
          {rows.map((row) => {
            const overdue = row.dueDate < today;
            return (
              <li
                key={row.payableId}
                className={cn(
                  "flex flex-wrap items-center justify-between gap-x-3 gap-y-1 rounded-lg px-2 py-2 text-sm",
                  // Past its date: a red start edge instead of a fill (13.2i).
                  overdue && "border-s-[3px] border-s-destructive",
                )}
              >
                <div className="space-y-0.5">
                  <p className="font-medium">{row.payee}</p>
                  <p className="text-xs text-muted-foreground">
                    {kindText(row)} — {row.description}
                  </p>
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
      <Link
        to={paths.payables}
        className="inline-flex items-center gap-1 text-sm font-medium tone-ink hover:underline"
      >
        همهٔ چک‌ها و قسط‌ها
        <ArrowLeft aria-hidden className="size-4" />
      </Link>
    </ListFrame>
  );
}

/**
 * Who the Owner should call today (BUSINESS_RULES.md §12 *Needs attention*), whatever range the
 * dashboard shows. One member can be on more than one list: each list is its own question. The
 * absence SMS's effect since the first message takes the place «بدهی قدیمی» had, which moved to
 * the end (asked by the developer, 1405/08/04, §12 *The absence SMS's effect*); it comes with its
 * own request.
 */
export function NeedsAttentionPanel({
  data,
  absenceSms,
}: {
  data: NeedsAttention;
  absenceSms?: AbsenceSmsEffect;
}) {
  const withoutMember = Number(data.oldDebtWithoutMember);

  return (
    // A `Card edge` in red, like the cards on every other page (13.2i).
    <Card edge style={toneStyle("red")}>
      <CardHeader className="grid-cols-[auto_1fr] gap-x-3">
        <span
          aria-hidden
          className="row-span-2 grid size-10 place-items-center rounded-xl tone-soft tone-ink"
        >
          <BellRing className="size-5" />
        </span>
        <CardTitle className="self-end text-lg">نیاز به اقدام</CardTitle>
        <CardDescription>
          کسانی که امروز باید با آن‌ها تماس گرفت، و چک و قسط‌هایی که باید پرداخت شوند.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3 lg:grid-cols-2">
        {/* The site's 300 code SMS a day are used up (§15): with a few hundred members that is an
            attack, not a busy day, so it comes first and across both columns. */}
        {data.signInCodeLimitReached && (
          <Alert variant="destructive" role="alert" className="lg:col-span-2">
            <p>
              سقف روزانهٔ پیامک کد ورود سایت امروز پر شده است؛ تا فردا برای کسی کد ورود فرستاده
              نمی‌شود.{" "}
              <Link to={paths.smsMessages} className="font-medium underline">
                پیامک‌ها
              </Link>
            </p>
          </Alert>
        )}
        <AttentionList
          id="running-out"
          icon={Hourglass}
          tone="amber"
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
          icon={UserMinus}
          tone="red"
          title={`رفته در ${windowDays} روز اخیر`}
          rule={`آخرین پلنش در ${windowDays} روز گذشته تمام شده و پلن دیگری ندارد.`}
          rows={data.left}
          detail={(row) => <p className="text-muted-foreground">پایان {formatDate(row.endedOn)}</p>}
        />
        <AttentionList
          id="absent"
          icon={UserX}
          tone="sky"
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
        {absenceSms !== undefined && <AbsenceSmsOverallBox effect={absenceSms} />}
        <PayablesDueList rows={data.payablesDue} today={data.today} />
        <div className="space-y-2">
          <AttentionList
            id="old-debts"
            icon={Coins}
            tone="orange"
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
      </CardContent>
    </Card>
  );
}
