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

const variants: Record<Subscription["status"], "success" | "secondary" | "destructive"> = {
  Active: "success",
  Upcoming: "secondary",
  Frozen: "secondary",
  Expired: "destructive",
  Exhausted: "destructive",
  Cancelled: "destructive",
};

/** BUSINESS_RULES.md §4: the subscription's calculated status. */
export function SubscriptionStatusBadge({ status }: { status: Subscription["status"] }) {
  return <Badge variant={variants[status]}>{labels[status]}</Badge>;
}
