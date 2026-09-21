import type { Subscription } from "../api";
import { SubscriptionHistoryRow } from "./SubscriptionHistoryRow";

interface SubscriptionHistoryTableProps {
  subscriptions: Subscription[];
  isOwner: boolean;
  onActionDone: (message: string) => void;
}

/**
 * Every subscription a member has ever had, newest first (task 4.5). Payment, freeze, unfreeze,
 * cancel and refund all live on each row (task 4.6 follow-up), not on a single "current
 * subscription" card — a member can have more than one subscription that still needs managing at
 * once (an active one plus a queued renewal), so every row keeps its own actions.
 */
export function SubscriptionHistoryTable({
  subscriptions,
  isOwner,
  onActionDone,
}: SubscriptionHistoryTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">پلن</th>
            <th className="py-2 text-start font-medium">شروع</th>
            <th className="py-2 text-start font-medium">پایان</th>
            <th className="py-2 text-start font-medium">جلسات</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">پرداخت</th>
            <th className="py-2 text-start font-medium">
              <span className="sr-only">عملیات</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {subscriptions.map((subscription) => (
            <SubscriptionHistoryRow
              key={subscription.id}
              subscription={subscription}
              isOwner={isOwner}
              onDone={onActionDone}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}
