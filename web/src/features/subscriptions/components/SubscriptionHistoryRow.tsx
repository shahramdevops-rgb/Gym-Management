import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { TableCell, TableRow } from "@/components/ui/table";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { RegisterPaymentForm } from "@/features/payments/components/RegisterPaymentForm";
import { RegisterRefundForm } from "@/features/payments/components/RegisterRefundForm";
import { stateEdge } from "@/lib/edges";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatDateTime, formatMoney, formatNumber } from "@/lib/format";
import { isPositiveMoney, subtractMoney } from "@/lib/money";
import { cn } from "@/lib/utils";

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
      <TableRow joins={hasPanel ? "below" : undefined}>
        {/* Edged at its start in the page's tone, like the history's rows (13.2j). */}
        <TableCell className="tone-row-edge">{label}</TableCell>
        {/* When it was sold, paid or not: payments carry their own times in «پرداخت‌ها». */}
        <TableCell label="تاریخ فروش">{formatDateTime(subscription.createdAt)}</TableCell>
        {queued ? (
          // A queued plan's dates move if the plan before it ends early or is frozen (§4); only
          // its length is fixed. The stored start is still shown, as where it stands today.
          <>
            <TableCell label="شروع">
              <div>بعد از پلن قبلی</div>
              <div className="text-xs text-muted-foreground">
                فعلاً {formatDate(subscription.startDate)}
              </div>
            </TableCell>
            <TableCell label="پایان">{`${formatNumber(Number(subscription.durationDays))} روز از شروع`}</TableCell>
          </>
        ) : (
          <>
            <TableCell label="شروع">{formatDate(subscription.startDate)}</TableCell>
            <TableCell label="پایان">{formatDate(subscription.endDate)}</TableCell>
          </>
        )}
        {/* Remaining of total, as everywhere a session count is shown (see `SessionsBar`). */}
        <TableCell
          label="جلسات"
          className="whitespace-nowrap"
        >{`${formatNumber(Number(subscription.remainingSessions))} از ${formatNumber(Number(subscription.totalSessions))} مانده`}</TableCell>
        <TableCell label="وضعیت">
          <SubscriptionStatusBadge status={subscription.status} />
        </TableCell>
        <TableCell label="پرداخت">
          <div className="flex items-center gap-2">
            <PaymentStatusBadge status={subscription.paymentStatus} edged />
            <span className="whitespace-nowrap text-muted-foreground">
              {formatMoney(subscription.netPaid)}
            </span>
          </div>
        </TableCell>
        <TableCell>
          {/* Edged at their end in a state's colour, like the cafe's orders (13.2j): green to pay,
              red to cancel or give money back; freezing keeps its plain outline. */}
          <div className="flex flex-wrap gap-1">
            {canPay && (
              <Button
                size="sm"
                variant="ghost"
                className={cn(stateEdge.success, "hover:bg-success/10 hover:text-success-ink")}
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
                variant="ghost"
                className={cn(
                  stateEdge.destructive,
                  "hover:bg-destructive/10 hover:text-destructive",
                )}
                aria-label={`لغو اشتراک ${context}`}
                onClick={() => toggle("cancel")}
              >
                لغو اشتراک
              </Button>
            )}
            {canRefund && (
              <Button
                size="sm"
                variant="ghost"
                className={cn(
                  stateEdge.destructive,
                  "hover:bg-destructive/10 hover:text-destructive",
                )}
                aria-label={`استرداد برای ${context}`}
                onClick={() => toggle("refund")}
              >
                استرداد
              </Button>
            )}
          </div>
        </TableCell>
      </TableRow>
      {hasPanel && (
        <TableRow joins="above" className="bg-muted/30 hover:bg-muted/30">
          <TableCell colSpan={8} className="space-y-2">
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
          </TableCell>
        </TableRow>
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
