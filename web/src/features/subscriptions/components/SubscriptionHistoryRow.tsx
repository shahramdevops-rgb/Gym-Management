import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { RegisterPaymentForm } from "@/features/payments/components/RegisterPaymentForm";
import { RegisterRefundForm } from "@/features/payments/components/RegisterRefundForm";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatDateTime, formatMoney, formatNumber } from "@/lib/format";
import { isPositiveMoney, subtractMoney } from "@/lib/money";

import { useFreezeSubscription, useUnfreezeSubscription, type Subscription } from "../api";
import { planLabel } from "../planLabel";
import { CancelSubscriptionForm } from "./CancelSubscriptionForm";
import { ConfirmFreezeDialog, type FreezeAction } from "./ConfirmFreezeDialog";
import { SubscriptionStatusBadge } from "./SubscriptionStatusBadge";

type RowAction = "payment" | "refund" | "cancel" | null;

interface SubscriptionHistoryRowProps {
  subscription: Subscription;
  /** Waiting behind another plan, so its dates are provisional (see `isQueuedBehindAnother`). */
  queued: boolean;
  isOwner: boolean;
  onDone: (message: string) => void;
}

/**
 * One row of the subscription history table, with its own actions (task 4.6 follow-up). Every
 * action targets THIS subscription by id, never "whichever one is newest": a member can have more
 * than one live subscription at once (an active one plus a queued renewal), and freezing, for
 * example, must still reach the active one even when it is no longer the newest row.
 */
export function SubscriptionHistoryRow({
  subscription,
  queued,
  isOwner,
  onDone,
}: SubscriptionHistoryRowProps) {
  const [action, setAction] = useState<RowAction>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState<FreezeAction | null>(null);
  const freeze = useFreezeSubscription();
  const unfreeze = useUnfreezeSubscription();

  function toggle(next: Exclude<RowAction, null>) {
    setError(null);
    setAction((current) => (current === next ? null : next));
  }

  function done(message: string) {
    setAction(null);
    onDone(message);
  }

  function askToConfirm(next: FreezeAction) {
    setError(null);
    setConfirming(next);
  }

  // Sent only from the confirmation box. A refusal closes the box and shows under the row, where
  // every other action on this subscription shows its errors.
  async function confirmFreezeAction() {
    const freezing = confirming === "freeze";
    try {
      if (freezing) {
        await freeze.mutateAsync({ id: subscription.id });
      } else {
        await unfreeze.mutateAsync({ id: subscription.id });
      }
      setConfirming(null);
      onDone(freezing ? "اشتراک فریز شد." : "فریز اشتراک برداشته شد.");
    } catch (problem) {
      setConfirming(null);
      setError(errorMessage(problem));
    }
  }

  const label = planLabel(subscription);
  const context = `${label} (${formatDate(subscription.startDate)})`;
  const remaining = subtractMoney(subscription.price, subscription.netPaid);
  const hasPanel = action !== null || error !== null;

  // Only the actions that would actually be allowed, so a finished, settled subscription shows no
  // buttons at all instead of a row of disabled ones (task 4.7). Each condition is the rule the
  // API would answer with: payment while anything is still owed, whatever the status
  // (BUSINESS_RULES.md §5 Member debt); cancel and refund only while nobody has used it (§4, §5).
  const unused = subscription.usedSessions === 0;
  const canPay = subscription.paymentStatus !== "Paid";
  const canFreeze = isOwner && subscription.status === "Active";
  const canUnfreeze = isOwner && subscription.status === "Frozen";
  const canCancel =
    isOwner && unused && ["Upcoming", "Active", "Frozen"].includes(subscription.status);
  const canRefund = isOwner && unused && isPositiveMoney(subscription.netPaid);

  return (
    <>
      <tr className="border-b">
        <td className="py-2">{label}</td>
        {/* When it was sold, paid or not: payments carry their own times in «پرداخت‌ها». */}
        <td className="py-2">{formatDateTime(subscription.createdAt)}</td>
        {queued ? (
          // A queued plan's dates move if the plan before it ends early or is frozen (§4); only
          // its length is fixed. The stored start is still shown, as where it stands today.
          <>
            <td className="py-2">
              <div>بعد از پلن قبلی</div>
              <div className="text-xs text-muted-foreground">
                فعلاً {formatDate(subscription.startDate)}
              </div>
            </td>
            <td className="py-2">{`${formatNumber(Number(subscription.durationDays))} روز از شروع`}</td>
          </>
        ) : (
          <>
            <td className="py-2">{formatDate(subscription.startDate)}</td>
            <td className="py-2">{formatDate(subscription.endDate)}</td>
          </>
        )}
        <td className="py-2">{`${subscription.usedSessions} / ${subscription.totalSessions}`}</td>
        <td className="py-2">
          <SubscriptionStatusBadge status={subscription.status} />
        </td>
        <td className="py-2">
          <div className="flex items-center gap-2">
            <PaymentStatusBadge status={subscription.paymentStatus} />
            <span className="text-muted-foreground">{formatMoney(subscription.netPaid)}</span>
          </div>
        </td>
        <td className="py-2">
          <div className="flex flex-wrap gap-1">
            {canPay && (
              <Button
                size="sm"
                variant="outline"
                aria-label={`ثبت پرداخت برای ${context}`}
                onClick={() => toggle("payment")}
              >
                ثبت پرداخت
              </Button>
            )}
            {canFreeze && (
              <Button
                size="sm"
                variant="outline"
                aria-label={`فریز ${context}`}
                disabled={freeze.isPending}
                onClick={() => askToConfirm("freeze")}
              >
                فریز
              </Button>
            )}
            {canUnfreeze && (
              <Button
                size="sm"
                variant="outline"
                aria-label={`رفع فریز ${context}`}
                disabled={unfreeze.isPending}
                onClick={() => askToConfirm("unfreeze")}
              >
                رفع فریز
              </Button>
            )}
            {canCancel && (
              <Button
                size="sm"
                variant="destructive"
                aria-label={`لغو اشتراک ${context}`}
                onClick={() => toggle("cancel")}
              >
                لغو اشتراک
              </Button>
            )}
            {canRefund && (
              <Button
                size="sm"
                variant="outline"
                aria-label={`استرداد برای ${context}`}
                onClick={() => toggle("refund")}
              >
                استرداد
              </Button>
            )}
          </div>
        </td>
      </tr>
      {hasPanel && (
        <tr className="border-b bg-muted/30">
          <td colSpan={8} className="space-y-2 py-2">
            <p className="text-xs text-muted-foreground">
              برای «{label}» —{" "}
              {queued
                ? `بعد از پلن قبلی (فعلاً از ${formatDate(subscription.startDate)})`
                : `از ${formatDate(subscription.startDate)} تا ${formatDate(subscription.endDate)}`}{" "}
              — قیمت {formatMoney(subscription.price)}، پرداخت‌شده{" "}
              {formatMoney(subscription.netPaid)}
              {isPositiveMoney(remaining) && <> — مانده {formatMoney(remaining)}</>}
            </p>
            {error !== null && <Alert variant="destructive">{error}</Alert>}
            {action === "payment" && (
              <RegisterPaymentForm
                subscriptionId={subscription.id}
                onDone={() => done("پرداخت ثبت شد.")}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "refund" && (
              <RegisterRefundForm
                subscriptionId={subscription.id}
                onDone={() => done("استرداد ثبت شد.")}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "cancel" && (
              <CancelSubscriptionForm
                subscriptionId={subscription.id}
                onDone={() => done("اشتراک لغو شد.")}
                onCancel={() => setAction(null)}
              />
            )}
          </td>
        </tr>
      )}
      <ConfirmFreezeDialog
        action={confirming}
        context={context}
        totalFrozenDays={Number(subscription.totalFrozenDays)}
        pending={freeze.isPending || unfreeze.isPending}
        onConfirm={() => void confirmFreezeAction()}
        onCancel={() => setConfirming(null)}
      />
    </>
  );
}
