import { Badge } from "@/components/ui/badge";
import { stateEdge } from "@/lib/edges";
import { cn } from "@/lib/utils";

import type { Subscription } from "../api";

const labels: Record<Subscription["status"], string> = {
  Active: "فعال",
  Upcoming: "در انتظار شروع",
  Frozen: "فریز",
  Expired: "منقضی",
  Exhausted: "جلسات تمام‌شده",
  Cancelled: "لغوشده",
};

/**
 * Edged like the other state tags (13.2j): green while it runs; amber once its sessions are used
 * up, like «پلن تمام‌شده» on the members' list; red when it was cancelled; grey for what waits,
 * is frozen or is simply over.
 */
const classes: Record<Subscription["status"], string> = {
  Active: cn(stateEdge.success, "font-semibold"),
  Upcoming: stateEdge.muted,
  Frozen: stateEdge.muted,
  Expired: stateEdge.muted,
  Exhausted: stateEdge.warning,
  Cancelled: cn(stateEdge.destructive, "font-semibold"),
};

/** BUSINESS_RULES.md §4: the subscription's calculated status. */
export function SubscriptionStatusBadge({ status }: { status: Subscription["status"] }) {
  return (
    <Badge variant="ghost" className={classes[status]}>
      {labels[status]}
    </Badge>
  );
}
