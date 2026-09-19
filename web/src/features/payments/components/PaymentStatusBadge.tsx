import { Badge } from "@/components/ui/badge";

import type { Payment } from "../api";

const labels: Record<Payment["subscriptionPaymentStatus"], string> = {
  Paid: "پرداخت‌شده",
  Partial: "پرداخت جزئی",
  Unpaid: "پرداخت‌نشده",
};

const variants: Record<Payment["subscriptionPaymentStatus"], "success" | "secondary" | "destructive"> = {
  Paid: "success",
  Partial: "secondary",
  Unpaid: "destructive",
};

/** BUSINESS_RULES.md §4 "Payment status (calculated)". */
export function PaymentStatusBadge({ status }: { status: Payment["subscriptionPaymentStatus"] }) {
  return <Badge variant={variants[status]}>{labels[status]}</Badge>;
}
