import { Badge } from "@/components/ui/badge";
import { stateEdge } from "@/lib/edges";
import { cn } from "@/lib/utils";

import type { Payment } from "../api";

const labels: Record<Payment["targetPaymentStatus"], string> = {
  Paid: "پرداخت‌شده",
  Partial: "پرداخت جزئی",
  Unpaid: "پرداخت‌نشده",
};

const variants: Record<Payment["targetPaymentStatus"], "success" | "secondary" | "destructive"> = {
  Paid: "success",
  Partial: "secondary",
  Unpaid: "destructive",
};

/**
 * The edged look (13.2i, asked by the developer, on trial in the history): no fill, a faint frame
 * and a thick end edge like a locker door, in the same colour the filled badge has.
 */
const edgedClasses: Record<Payment["targetPaymentStatus"], string> = {
  Paid: cn(stateEdge.success, "font-semibold"),
  Partial: stateEdge.muted,
  Unpaid: cn(stateEdge.destructive, "font-semibold"),
};

/** BUSINESS_RULES.md §4 "Payment status (calculated)". */
export function PaymentStatusBadge({
  status,
  edged = false,
}: {
  status: Payment["targetPaymentStatus"];
  /** Edged instead of filled, like the history's «داخل باشگاه» tag. */
  edged?: boolean;
}) {
  if (edged) {
    return (
      <Badge variant="ghost" className={edgedClasses[status]}>
        {labels[status]}
      </Badge>
    );
  }

  return <Badge variant={variants[status]}>{labels[status]}</Badge>;
}
