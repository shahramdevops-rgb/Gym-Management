import { Table, TableBody, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { Subscription } from "../api";
import { isQueuedBehindAnother } from "../queue";
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
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>پلن</TableHead>
          <TableHead>تاریخ فروش</TableHead>
          <TableHead>شروع</TableHead>
          <TableHead>پایان</TableHead>
          <TableHead>جلسات</TableHead>
          <TableHead>وضعیت</TableHead>
          <TableHead>پرداخت</TableHead>
          <TableHead>
            <span className="sr-only">عملیات</span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {subscriptions.map((subscription) => (
          <SubscriptionHistoryRow
            key={subscription.id}
            subscription={subscription}
            queued={isQueuedBehindAnother(subscription, subscriptions)}
            isOwner={isOwner}
            onDone={onActionDone}
          />
        ))}
      </TableBody>
    </Table>
  );
}
