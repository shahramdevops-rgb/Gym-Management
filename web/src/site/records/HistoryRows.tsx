import type { ReactNode } from "react";

import { Badge } from "@/components/ui/badge";
import {
  emptyValue,
  formatDate,
  formatDateTime,
  formatMoney,
  formatTime,
  toPersianDigits,
} from "@/lib/format";

import type { MyCafeOrder, MyPayment, MyVisit, SitePlan } from "./api";
import {
  cafeItemsLine,
  cafeLabel,
  paymentMethodLabels,
  paymentStatus,
  planLabel,
  planStatus,
  serviceLabel,
  type PaymentStatus,
} from "./labels";

/*
 * One row of each history, made for a phone: what it was and its tags on the first line, the
 * rest in a muted line under it, the amount at the end. A cancelled row is marked, never
 * explained: who cancelled it and why are the staff's (BUSINESS_RULES.md §15 *The member's own
 * records*), and the API does not send them.
 */

function Row({
  title,
  tags,
  detail,
  end,
}: {
  title: ReactNode;
  tags?: ReactNode;
  detail: ReactNode;
  end?: ReactNode;
}) {
  return (
    <li className="flex items-start justify-between gap-3 py-3 first:pt-0 last:pb-0">
      <div className="min-w-0 space-y-1">
        <p className="flex flex-wrap items-center gap-1.5 font-medium">
          {title}
          {tags}
        </p>
        <div className="text-sm text-muted-foreground">{detail}</div>
      </div>
      {end !== undefined && <div className="shrink-0 text-sm font-medium">{end}</div>}
    </li>
  );
}

function PaymentStatusTag({ status }: { status: PaymentStatus }) {
  const paid = paymentStatus[status];

  return <Badge variant={paid.tone}>{paid.label}</Badge>;
}

const cancelledTag = <Badge variant="destructive">لغو شده</Badge>;

/** A check-in, its check-out and locker, its tags, and what the visit was charged. */
export function VisitRow({ visit }: { visit: MyVisit }) {
  const parts = [
    visit.checkedOutAt === null ? null : `خروج ${formatTime(visit.checkedOutAt)}`,
    visit.lockerNumber === null ? null : `کمد ${toPersianDigits(visit.lockerNumber)}`,
  ].filter((part) => part !== null);

  return (
    <Row
      title={formatDateTime(visit.checkedInAt)}
      tags={
        <>
          {visit.cancelledAt !== null ? (
            cancelledTag
          ) : visit.autoClosedAt !== null ? (
            <Badge variant="secondary">بسته خودکار</Badge>
          ) : (
            visit.checkedOutAt === null && <Badge variant="success">داخل باشگاه</Badge>
          )}
          {/* No session was used for either (§7). */}
          {visit.isCardioOnly && <Badge variant="cardio">فقط هوازی</Badge>}
          {visit.isBirthdayVisit && <Badge variant="birthday">تولد</Badge>}
        </>
      }
      detail={
        <>
          <p>{parts.length === 0 ? emptyValue : parts.join(" · ")}</p>
          {visit.charges.map((charge) => (
            <p key={charge.id} className="mt-1 flex flex-wrap items-center gap-1.5">
              {serviceLabel(charge.kind, charge.description)}: {formatMoney(charge.amount)}
              <PaymentStatusTag status={charge.paymentStatus} />
            </p>
          ))}
        </>
      }
    />
  );
}

/** A plan: its days and sessions, its dates, and what is paid of it. */
export function PlanRow({ plan }: { plan: SitePlan }) {
  const status = planStatus[plan.status];

  return (
    <Row
      title={planLabel(plan)}
      tags={<Badge variant={status.tone}>{status.label}</Badge>}
      detail={
        <>
          <p>
            {formatDate(plan.startDate)} تا {formatDate(plan.endDate)} ·{" "}
            {toPersianDigits(plan.usedSessions)} از {toPersianDigits(plan.totalSessions)} جلسه
            استفاده شده
          </p>
          {Number(plan.totalFrozenDays) > 0 && (
            <p>{toPersianDigits(plan.totalFrozenDays)} روز فریز</p>
          )}
          <p className="mt-1 flex flex-wrap items-center gap-1.5">
            <PaymentStatusTag status={plan.paymentStatus} />
            {formatMoney(plan.netPaid)} از {formatMoney(plan.price)}
          </p>
        </>
      }
    />
  );
}

/** What a payment went against, said as the panel's payment history says it. */
function paidForLabel(payment: MyPayment): string {
  switch (payment.targetKind) {
    case "Subscription":
      return payment.subscriptionPlan === null
        ? "اشتراک"
        : `اشتراک ${planLabel(payment.subscriptionPlan)}`;
    case "ServiceCharge":
      return serviceLabel(payment.serviceKind, payment.serviceDescription);
    case "CafeOrder":
      return cafeLabel;
  }
}

/**
 * A payment or a refund. One of a «تسویه یکجا» says so: the desk took one amount for several
 * things, and this row is this thing's part of it (§5).
 */
export function PaymentRow({ payment }: { payment: MyPayment }) {
  const refund = payment.kind === "Refund";
  const parts = [
    formatDateTime(payment.paidAt),
    paymentMethodLabels[payment.method],
    payment.referenceNumber === null ? null : `پیگیری ${toPersianDigits(payment.referenceNumber)}`,
  ].filter((part) => part !== null);

  return (
    <Row
      title={paidForLabel(payment)}
      tags={refund && <Badge variant="destructive">بازگشت وجه</Badge>}
      detail={
        <>
          <p>{parts.join(" · ")}</p>
          {payment.settlement !== null && (
            <p>بخشی از پرداخت یکجای {formatMoney(payment.settlement.total)}</p>
          )}
        </>
      }
      end={refund ? `−${formatMoney(payment.amount)}` : formatMoney(payment.amount)}
    />
  );
}

/** A cafe purchase: its lines, and what is paid of it unless it was cancelled. */
export function CafeOrderRow({ order }: { order: MyCafeOrder }) {
  const cancelled = order.cancelledAt !== null;

  return (
    <Row
      title={formatDate(order.orderedOn)}
      tags={cancelled ? cancelledTag : <PaymentStatusTag status={order.paymentStatus} />}
      detail={
        <>
          <p>{cafeItemsLine(order.items)}</p>
          {!cancelled && Number(order.outstanding) > 0 && (
            <p>مانده: {formatMoney(order.outstanding)}</p>
          )}
        </>
      }
      end={formatMoney(order.totalAmount)}
    />
  );
}
