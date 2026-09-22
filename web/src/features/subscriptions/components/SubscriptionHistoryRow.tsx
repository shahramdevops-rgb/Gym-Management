import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { RegisterPaymentForm } from "@/features/payments/components/RegisterPaymentForm";
import { RegisterRefundForm } from "@/features/payments/components/RegisterRefundForm";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney } from "@/lib/format";

import { useFreezeSubscription, useUnfreezeSubscription, type Subscription } from "../api";
import { CancelSubscriptionForm } from "./CancelSubscriptionForm";
import { SubscriptionStatusBadge } from "./SubscriptionStatusBadge";

type RowAction = "payment" | "refund" | "cancel" | null;

interface SubscriptionHistoryRowProps {
  subscription: Subscription;
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
  isOwner,
  onDone,
}: SubscriptionHistoryRowProps) {
  const [action, setAction] = useState<RowAction>(null);
  const [error, setError] = useState<string | null>(null);
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

  async function doFreeze() {
    setError(null);
    try {
      await freeze.mutateAsync({ id: subscription.id });
      onDone("اشتراک فریز شد.");
    } catch (problem) {
      setError(errorMessage(problem));
    }
  }

  async function doUnfreeze() {
    setError(null);
    try {
      await unfreeze.mutateAsync({ id: subscription.id });
      onDone("فریز اشتراک برداشته شد.");
    } catch (problem) {
      setError(errorMessage(problem));
    }
  }

  const context = `${subscription.planName} (${formatDate(subscription.startDate)})`;
  const remaining = Number(subscription.price) - Number(subscription.netPaid);
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
  const canRefund = isOwner && unused && Number(subscription.netPaid) > 0;

  return (
    <>
      <tr className="border-b">
        <td className="py-2">{subscription.planName}</td>
        <td className="py-2">{formatDate(subscription.startDate)}</td>
        <td className="py-2">{formatDate(subscription.endDate)}</td>
        <td className="py-2">
          {subscription.totalSessions === null
            ? "نامحدود"
            : `${subscription.usedSessions} / ${subscription.totalSessions}`}
        </td>
        <td className="py-2">
          <SubscriptionStatusBadge status={subscription.status} />
        </td>
        <td className="py-2">
          <div className="flex items-center gap-2">
            <PaymentStatusBadge status={subscription.paymentStatus} />
            <span className="text-muted-foreground">
              {formatMoney(Number(subscription.netPaid))}
            </span>
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
                onClick={() => void doFreeze()}
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
                onClick={() => void doUnfreeze()}
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
          <td colSpan={7} className="space-y-2 py-2">
            <p className="text-xs text-muted-foreground">
              برای «{subscription.planName}» — از {formatDate(subscription.startDate)} تا{" "}
              {formatDate(subscription.endDate)} — قیمت {formatMoney(Number(subscription.price))}،
              پرداخت‌شده {formatMoney(Number(subscription.netPaid))}
              {remaining > 0 && <> — مانده {formatMoney(remaining)}</>}
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
    </>
  );
}
