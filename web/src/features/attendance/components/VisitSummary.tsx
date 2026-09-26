import { Alert } from "@/components/ui/alert";
import { useMemberDebt } from "@/features/members/api";
import { debtItemLabel } from "@/features/members/debtItemLabel";
import { useCurrentSubscription, type Subscription } from "@/features/subscriptions/api";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney, toPersianDigits } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";
import { cn } from "@/lib/utils";

import { lowSessionsThreshold } from "./CurrentlyInsideTable";

/**
 * What the desk should know while the member is standing there (BUSINESS_RULES.md §7 *Confirming
 * at the front desk*): the plan, the sessions left, and the debt item by item. Shown after a
 * check-in and before a check-out.
 *
 * Both come from their own queries rather than the check-in response, so the numbers are the ones
 * after this visit: the session it just used, and the single visit it may just have sold.
 */
export function VisitSummary({ memberId }: { memberId: string }) {
  return (
    <div className="space-y-3">
      <SubscriptionLine memberId={memberId} />
      <DebtBox memberId={memberId} />
    </div>
  );
}

function SubscriptionLine({ memberId }: { memberId: string }) {
  const subscription = useCurrentSubscription(memberId);

  if (subscription.isPending) {
    return <p className="text-sm text-muted-foreground">در حال بارگذاری اشتراک…</p>;
  }
  if (subscription.isError) {
    return <Alert variant="destructive">{errorMessage(subscription.error)}</Alert>;
  }
  if (subscription.data === null) {
    return <p className="text-sm text-muted-foreground">اشتراکی ثبت نشده است.</p>;
  }

  const current = subscription.data;

  return (
    <dl className="grid grid-cols-2 gap-3 rounded-lg border p-3 text-sm">
      <div>
        <dt className="text-muted-foreground">پلن</dt>
        <dd className="font-medium">{current.planName}</dd>
      </div>
      <div>
        <dt className="text-muted-foreground">جلسات باقی‌مانده</dt>
        <dd className={cn("font-medium", isLow(current) && "text-destructive")}>
          {sessionsLeft(current)}
        </dd>
      </div>
      <div className="col-span-2">
        <dt className="text-muted-foreground">اعتبار تا</dt>
        <dd className="font-medium">{formatDate(current.endDate)}</dd>
      </div>
    </dl>
  );
}

function sessionsLeft(subscription: Subscription): string {
  if (subscription.isSingleSession) {
    return "تک‌جلسه‌ای";
  }
  if (subscription.totalSessions === null || subscription.remainingSessions === null) {
    return "نامحدود";
  }
  return `${toPersianDigits(subscription.remainingSessions)} از ${toPersianDigits(subscription.totalSessions)} جلسه`;
}

/** The front desk board's threshold (BUSINESS_RULES.md §7), so both screens flag the same members. */
function isLow(subscription: Subscription): boolean {
  return (
    !subscription.isSingleSession &&
    subscription.remainingSessions !== null &&
    Number(subscription.remainingSessions) <= lowSessionsThreshold
  );
}

/**
 * The part the developer called the most important: what is still owed, and for what. It is never
 * shown as a bare total (BUSINESS_RULES.md §5 *Member debt*), and it is large and red so it is
 * read, not skimmed.
 */
function DebtBox({ memberId }: { memberId: string }) {
  const debt = useMemberDebt(memberId);

  if (debt.isPending) {
    return <p className="text-sm text-muted-foreground">در حال بارگذاری بدهی…</p>;
  }
  if (debt.isError) {
    return <Alert variant="destructive">{errorMessage(debt.error)}</Alert>;
  }
  if (!isPositiveMoney(debt.data.total)) {
    return (
      <Alert variant="success" role="status">
        این عضو بدهی ندارد.
      </Alert>
    );
  }

  return (
    <section
      aria-label="بدهی"
      className="space-y-2 rounded-lg border-2 border-destructive bg-destructive/5 p-4 text-destructive"
    >
      <p className="text-sm font-medium">بدهی این عضو</p>
      <p className="text-2xl font-bold">{formatMoney(debt.data.total)}</p>
      <ul className="space-y-1 border-t border-destructive/30 pt-2 text-sm">
        {debt.data.items.map((item) => (
          <li key={item.id} className="flex justify-between gap-3">
            <span>
              {debtItemLabel(item)}
              <span className="text-destructive/70"> · {formatDate(item.startDate)}</span>
            </span>
            <span className="font-medium">{formatMoney(item.outstanding)}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
