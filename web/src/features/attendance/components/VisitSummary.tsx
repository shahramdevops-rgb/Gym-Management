import { Alert } from "@/components/ui/alert";
import { useVisitCafeOrders } from "@/features/cafe/api";
import { useMemberDebt } from "@/features/members/api";
import { debtBySource } from "@/features/members/debtBySource";
import { debtItemLabel } from "@/features/members/debtItemLabel";
import { useCurrentSubscription, type Subscription } from "@/features/subscriptions/api";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney, toPersianDigits } from "@/lib/format";
import { addMoney, isPositiveMoney } from "@/lib/money";
import { cn } from "@/lib/utils";

import { lowSessionsThreshold } from "./CurrentlyInsideTable";

/**
 * What the desk should know while the member is standing there (BUSINESS_RULES.md §7 *Confirming
 * at the front desk*): the plan, the sessions left, and the debt item by item. Shown after a
 * check-in and before a check-out.
 *
 * Both come from their own queries rather than the check-in response, so the numbers are the ones
 * after this visit: the session it just used, and the single visit it may just have sold.
 *
 * At check-out it also lists what the visit bought from the cafe (`attendanceId`), so the member
 * hears what is on their account before they leave (BUSINESS_RULES.md §8).
 */
export function VisitSummary({
  memberId,
  attendanceId,
}: {
  memberId: string;
  /** The visit being closed, whose cafe purchases are listed; omitted at check-in. */
  attendanceId?: string;
}) {
  return (
    <div className="space-y-3">
      <SubscriptionLine memberId={memberId} />
      {attendanceId !== undefined && <VisitPurchases attendanceId={attendanceId} />}
      <DebtBox memberId={memberId} />
    </div>
  );
}

/** What this visit picked up at the cafe, line by line; nothing at all when it bought nothing. */
function VisitPurchases({ attendanceId }: { attendanceId: string }) {
  const orders = useVisitCafeOrders(attendanceId);

  if (orders.isPending || (orders.isSuccess && orders.data.length === 0)) {
    return null;
  }
  if (orders.isError) {
    return <Alert variant="destructive">{errorMessage(orders.error)}</Alert>;
  }

  const lines = orders.data.flatMap((order) => order.items);
  const total = addMoney(...orders.data.map((order) => order.totalAmount));

  return (
    <section aria-label="خریدهای بوفه" className="space-y-2 rounded-lg border p-3 text-sm">
      <p className="font-medium">خریدهای بوفه در این مراجعه</p>
      <ul className="space-y-1">
        {lines.map((line) => (
          <li key={line.id} className="flex justify-between gap-3">
            <span>
              {line.productName} × {toPersianDigits(line.quantity)}
            </span>
            <span>{formatMoney(line.lineTotal)}</span>
          </li>
        ))}
      </ul>
      <p className="flex justify-between gap-3 border-t pt-2 font-medium">
        <span>جمع</span>
        <span>{formatMoney(total)}</span>
      </p>
    </section>
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
      {/* By source first — plan, هوازی, cafe — the way the desk says it to the member. */}
      <dl aria-label="بدهی به تفکیک" className="space-y-1 border-t border-destructive/30 pt-2">
        {debtBySource(debt.data.items).map((source) => (
          <div key={source.label} className="flex justify-between gap-3 font-medium">
            <dt>{source.label}</dt>
            <dd>{formatMoney(source.amount)}</dd>
          </div>
        ))}
      </dl>
      <ul className="space-y-1 border-t border-destructive/30 pt-2 text-xs text-destructive/80">
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
