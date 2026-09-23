import { Badge } from "@/components/ui/badge";

import type { Payment } from "../api";

const labels: Record<Payment["targetPaymentStatus"], string> = {
  Paid: "پرداخت‌شده",
  Partial: "پرداخت جزئی",
  Unpaid: "پرداخت‌نشده",
};

const variants: Record<
  Payment["targetPaymentStatus"],
  "success" | "secondary" | "destructive"
> = {
  Paid: "success",
  Partial: "secondary",
  Unpaid: "destructive",
};

/** BUSINESS_RULES.md §4 "Payment status (calculated)". */
export function PaymentStatusBadge({ status }: { status: Payment["targetPaymentStatus"] }) {
  return <Badge variant={variants[status]}>{labels[status]}</Badge>;
}
