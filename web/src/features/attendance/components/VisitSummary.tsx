import { Alert } from "@/components/ui/alert";
import { useVisitCafeOrders, type CafeOrderItem } from "@/features/cafe/api";
import { useMemberDebt, type MemberDebt } from "@/features/members/api";
import { debtBySource } from "@/features/members/debtBySource";
import { debtItemLabel } from "@/features/members/debtItemLabel";
import { SettleDebt } from "@/features/payments/components/SettleDebt";
import { useCurrentSubscription, type Subscription } from "@/features/subscriptions/api";
import { planLabel } from "@/features/subscriptions/planLabel";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney, gymToday, toPersianDigits } from "@/lib/format";
import { addMoney, isPositiveMoney } from "@/lib/money";
import { cn } from "@/lib/utils";

import { currentlyInsideRefetchMs } from "../api";
import { daysUntil, expiringDaysThreshold, lowSessionsThreshold } from "../renewal";

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
  withSessions = true,
}: {
  memberId: string;
  /** The visit being closed, whose cafe purchases are listed; omitted at check-in. */
  attendanceId?: string;
  /** False where the caller already shows the sessions (the locker's box, with its bar). */
  withSessions?: boolean;
}) {
  return (
    <div className="space-y-3">
      <SubscriptionLine memberId={memberId} withSessions={withSessions} />
      {attendanceId !== undefined && <VisitPurchases attendanceId={attendanceId} />}
      <DebtBox memberId={memberId} itemized={attendanceId === undefined} />
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

  const lines = byProduct(orders.data.flatMap((order) => order.items));
  const total = addMoney(...orders.data.map((order) => order.totalAmount));

  return (
    <section aria-label="خریدهای بوفه" className="space-y-2 rounded-lg border p-3 text-sm">
      <p className="font-medium">خریدهای بوفه در این مراجعه</p>
      <ul className="space-y-1">
        {lines.map((line) => (
          <li key={line.productId} className="flex justify-between gap-3">
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

/**
 * One line per product, however many orders it came in: two espressos bought at the locker and at
 * the till are "× 2", the way the member would say it. The amounts are added, not recomputed, so a
 * price changed between the two orders still adds up to what was charged.
 */
function byProduct(items: CafeOrderItem[]) {
  const lines = new Map<
    string,
    { productId: string; productName: string; quantity: number; lineTotal: string }
  >();
  for (const item of items) {
    const line = lines.get(item.productId);
    lines.set(item.productId, {
      productId: item.productId,
      productName: item.productName,
      quantity: (line?.quantity ?? 0) + Number(item.quantity),
      lineTotal: addMoney(line?.lineTotal ?? "0", item.lineTotal),
    });
  }

  return [...lines.values()];
}

function SubscriptionLine({ memberId, withSessions }: { memberId: string; withSessions: boolean }) {
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
        <dd className="font-medium">{planLabel(current)}</dd>
      </div>
      <ValidityPeriod subscription={current} />
      {withSessions && (
        <div className="col-span-2">
          <dt className="text-muted-foreground">جلسات باقی‌مانده</dt>
          <dd className={cn("font-medium", isLow(current) && "text-destructive")}>
            {sessionsLeft(current)}
          </dd>
        </div>
      )}
    </dl>
  );
}

/**
 * The first and the last day the plan can be used, both inclusive (BUSINESS_RULES.md §4), and how
 * many days that leaves from the gym's today. The count turns red at the same threshold the desk
 * panel's renewal list uses, so both screens call the same plan "running out".
 */
function ValidityPeriod({ subscription }: { subscription: Subscription }) {
  const sameDay = subscription.startDate === subscription.endDate;

  return (
    <div>
      <dt className="text-muted-foreground">دوره اعتبار</dt>
      <dd className="font-medium">
        {sameDay
          ? formatDate(subscription.startDate)
          : `${formatDate(subscription.startDate)} تا ${formatDate(subscription.endDate)}`}
      </dd>
      <DaysLeft subscription={subscription} />
    </div>
  );
}

function DaysLeft({ subscription }: { subscription: Subscription }) {
  // A single visit is today only by design (§4); counting its days says nothing.
  if (subscription.isSingleSession) {
    return null;
  }

  const today = gymToday();
  if (subscription.startDate > today) {
    return <dd className="text-xs text-muted-foreground">هنوز شروع نشده</dd>;
  }

  const left = daysUntil(subscription.endDate, today);
  const text =
    left < 0 ? "تمام شده" : left === 0 ? "امروز تمام می‌شود" : `${toPersianDigits(left)} روز مانده`;

  return (
    <dd
      className={cn(
        "text-xs",
        left <= expiringDaysThreshold ? "font-medium text-destructive" : "text-muted-foreground",
      )}
    >
      {text}
    </dd>
  );
}

function sessionsLeft(subscription: Subscription): string {
  if (subscription.isSingleSession) {
    return "تک‌جلسه‌ای";
  }
  return `${toPersianDigits(subscription.remainingSessions)} از ${toPersianDigits(subscription.totalSessions)} جلسه`;
}

/** The front desk board's threshold (BUSINESS_RULES.md §7), so both screens flag the same members. */
function isLow(subscription: Subscription): boolean {
  return (
    !subscription.isSingleSession && Number(subscription.remainingSessions) <= lowSessionsThreshold
  );
}

/**
 * The part the developer called the most important: what is still owed, and for what. It is never
 * shown as a bare total (BUSINESS_RULES.md §5 *Member debt*), and it is large and red so it is
 * read, not skimmed.
 */
function DebtBox({ memberId, itemized }: { memberId: string; itemized: boolean }) {
  // Polled with the visit's purchases above it: an order from the till on another computer has to
  // reach «تسویه یکجا» as well, not only the list.
  const debt = useMemberDebt(memberId, { refetchInterval: currentlyInsideRefetchMs });

  if (debt.isPending) {
    return <p className="text-sm text-muted-foreground">در حال بارگذاری بدهی…</p>;
  }
  if (debt.isError) {
    return <Alert variant="destructive">{errorMessage(debt.error)}</Alert>;
  }

  return (
    <div className="space-y-3">
      <DebtDetails debt={debt.data} itemized={itemized} />
      {/* Collected here, while the member is standing at the desk (BUSINESS_RULES.md §5). */}
      <SettleDebt memberId={memberId} items={debt.data.items} />
    </div>
  );
}

/**
 * With a visit's purchases already listed above it (the locker's box, check-out), the debt stops at
 * the total by source: the same cafe lines twice over is noise the desk has to read past. At
 * check-in there is no such list, so the items are shown one by one.
 */
function DebtDetails({ debt, itemized }: { debt: MemberDebt; itemized: boolean }) {
  if (!isPositiveMoney(debt.total)) {
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
      <p className="text-2xl font-bold">{formatMoney(debt.total)}</p>
      {/* By source first — plan, هوازی, cafe — the way the desk says it to the member. */}
      <dl aria-label="بدهی به تفکیک" className="space-y-1 border-t border-destructive/30 pt-2">
        {debtBySource(debt.items).map((source) => (
          <div key={source.label} className="flex justify-between gap-3 font-medium">
            <dt>{source.label}</dt>
            <dd>{formatMoney(source.amount)}</dd>
          </div>
        ))}
      </dl>
      {itemized && (
        <ul
          aria-label="بدهی جزء به جزء"
          className="space-y-1 border-t border-destructive/30 pt-2 text-xs text-destructive/80"
        >
          {debt.items.map((item) => (
            <li key={item.id} className="flex justify-between gap-3">
              <span>
                {debtItemLabel(item)}
                <span className="text-destructive/70"> · {formatDate(item.startDate)}</span>
              </span>
              <span className="font-medium">{formatMoney(item.outstanding)}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
