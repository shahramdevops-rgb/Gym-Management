import { Badge } from "@/components/ui/badge";

import type { Payment } from "../api";

const labels: Record<Payment["targetPaymentStatus"], string> = {
  Paid: "پرداخت‌شده",
  Partial: "پرداخت جزئی",
  Unpaid: "پرداخت‌نشده",
};

/**
 * Solid on every screen (asked by the developer, 1405/07/22, replacing the edged look of 13.2i
 * that the history and the cafe's orders had).
 */
const variants: Record<Payment["targetPaymentStatus"], "success" | "secondary" | "destructive"> = {
  Paid: "success",
  Partial: "secondary",
  Unpaid: "destructive",
};

/** BUSINESS_RULES.md §4 "Payment status (calculated)". */
export function PaymentStatusBadge({ status }: { status: Payment["targetPaymentStatus"] }) {
  return <Badge variant={variants[status]}>{labels[status]}</Badge>;
}
