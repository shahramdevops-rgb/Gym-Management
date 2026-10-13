import { Badge } from "@/components/ui/badge";

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
 * Solid, like every state tag (asked by the developer, 1405/07/22; edged in 13.2j): green while it
 * runs; amber once its sessions are used up, like «پلن تمام‌شده» on the members' list; red when it
 * was cancelled; grey for what waits, is frozen or is simply over.
 */
const variants: Record<
  Subscription["status"],
  "success" | "secondary" | "warning" | "destructive"
> = {
  Active: "success",
  Upcoming: "secondary",
  Frozen: "secondary",
  Expired: "secondary",
  Exhausted: "warning",
  Cancelled: "destructive",
};

/** BUSINESS_RULES.md §4: the subscription's calculated status. */
export function SubscriptionStatusBadge({ status }: { status: Subscription["status"] }) {
  return <Badge variant={variants[status]}>{labels[status]}</Badge>;
}
