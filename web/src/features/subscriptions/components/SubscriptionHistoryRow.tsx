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
export function SubscriptionHistoryRow({ subscription, isOwner, onDone }: SubscriptionHistoryRowProps) {
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
            <span className="text-muted-foreground">{formatMoney(Number(subscription.netPaid))}</span>
          </div>
        </td>
        <td className="py-2">
          <div className="flex flex-wrap gap-1">
            <Button
              size="sm"
              variant="outline"
              aria-label={`ثبت پرداخت برای ${context}`}
              disabled={subscription.paymentStatus === "Paid"}
              onClick={() => toggle("payment")}
            >
              ثبت پرداخت
            </Button>
            {isOwner && (
              <>
                <Button
                  size="sm"
                  variant="outline"
                  aria-label={`فریز ${context}`}
                  disabled={subscription.status !== "Active" || freeze.isPending}
                  onClick={() => void doFreeze()}
                >
                  فریز
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  aria-label={`رفع فریز ${context}`}
                  disabled={subscription.status !== "Frozen" || unfreeze.isPending}
                  onClick={() => void doUnfreeze()}
                >
                  رفع فریز
                </Button>
                <Button
                  size="sm"
                  variant="destructive"
                  aria-label={`لغو اشتراک ${context}`}
                  disabled={subscription.status === "Cancelled"}
                  onClick={() => toggle("cancel")}
                >
                  لغو اشتراک
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  aria-label={`استرداد برای ${context}`}
                  onClick={() => toggle("refund")}
                >
                  استرداد
                </Button>
              </>
            )}
          </div>
        </td>
      </tr>
      {hasPanel && (
        <tr className="border-b bg-muted/30">
          <td colSpan={7} className="space-y-2 py-2">
            <p className="text-xs text-muted-foreground">
              برای «{subscription.planName}» — از {formatDate(subscription.startDate)} تا{" "}
              {formatDate(subscription.endDate)} — قیمت {formatMoney(Number(subscription.price))}، پرداخت‌شده{" "}
              {formatMoney(Number(subscription.netPaid))}
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
