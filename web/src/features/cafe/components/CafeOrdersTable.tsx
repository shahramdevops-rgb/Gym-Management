import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { guestLabel } from "@/features/attendance/holder";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { formatDate, formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";

import type { CafeOrder } from "../api";
import { CafeOrderPaymentForm } from "./CafeOrderPaymentForm";
import { CancelCafeOrderForm } from "./CancelCafeOrderForm";

interface CafeOrdersTableProps {
  orders: CafeOrder[];
  /** False on a member's own profile, where every row is the same person. */
  showCustomer?: boolean;
  onDone: (message: string) => void;
}

/** Orders newest first, as the API sends them; cancelled ones stay in the list, marked (§8). */
export function CafeOrdersTable({ orders, showCustomer = true, onDone }: CafeOrdersTableProps) {
  const columns = showCustomer ? 7 : 6;

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">تاریخ</th>
            {showCustomer && <th className="py-2 text-start font-medium">مشتری</th>}
            <th className="py-2 text-start font-medium">اقلام</th>
            <th className="py-2 text-start font-medium">مبلغ</th>
            <th className="py-2 text-start font-medium">پرداخت</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">عملیات</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((order) => (
            <CafeOrderRow
              key={order.id}
              order={order}
              showCustomer={showCustomer}
              columns={columns}
              onDone={onDone}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

type RowAction = "payment" | "cancel" | null;

interface CafeOrderRowProps {
  order: CafeOrder;
  showCustomer: boolean;
  columns: number;
  onDone: (message: string) => void;
}

/**
 * One order and what can still be done to it. Only the actions the API would accept are shown:
 * a payment while something is owed on a live order, a cancellation while it is not cancelled
 * (§8: a cancelled order takes no more payments and cannot be cancelled twice). An order is never
 * edited, so there is no edit button to look for.
 */
function CafeOrderRow({ order, showCustomer, columns, onDone }: CafeOrderRowProps) {
  const [action, setAction] = useState<RowAction>(null);

  const cancelled = order.cancelledAt !== null;
  const canPay = !cancelled && isPositiveMoney(order.outstanding);
  const canCancel = !cancelled;
  const context = `سفارش ${formatDate(order.orderedOn)} به مبلغ ${formatMoney(order.totalAmount)}`;

  function toggle(next: Exclude<RowAction, null>) {
    setAction((current) => (current === next ? null : next));
  }

  function done(message: string) {
    setAction(null);
    onDone(message);
  }

  return (
    <>
      <tr className="border-b align-top">
        <td className="py-2">{formatDate(order.orderedOn)}</td>
        {showCustomer && (
          <td className="py-2">
            {order.guestName !== null ? (
              // An order on a guest's visit (BUSINESS_RULES.md §7 *Guest visit*): their name, no profile.
              <span className="flex flex-wrap items-center gap-1">
                {order.guestName}
                <Badge variant="outline">
                  {!cancelled && isPositiveMoney(order.outstanding)
                    ? "پرداخت‌نشده — مهمان"
                    : guestLabel}
                </Badge>
              </span>
            ) : order.memberId === null ? (
              <span className="text-muted-foreground">مشتری آزاد</span>
            ) : (
              <Link
                to={paths.member(order.memberId)}
                className="underline-offset-4 hover:underline"
              >
                {order.memberFullName}
              </Link>
            )}
          </td>
        )}
        <td className="py-2">
          <ul>
            {order.items.map((item) => (
              <li key={item.id}>
                {item.productName} × {toPersianDigits(item.quantity)}
              </li>
            ))}
          </ul>
        </td>
        <td className="py-2">{formatMoney(order.totalAmount)}</td>
        <td className="py-2">
          <div className="flex flex-wrap items-center gap-2">
            {!cancelled && <PaymentStatusBadge status={order.paymentStatus} />}
            <span className="text-muted-foreground">{formatMoney(order.netPaid)}</span>
          </div>
        </td>
        <td className="py-2">
          {cancelled ? (
            <div className="space-y-1">
              <Badge variant="destructive">لغو شده</Badge>
              <p className="text-xs text-muted-foreground">
                {formatDateTime(order.cancelledAt)} — {order.cancelReason}
              </p>
            </div>
          ) : (
            isPositiveMoney(order.outstanding) && (
              <span className="text-destructive">مانده {formatMoney(order.outstanding)}</span>
            )
          )}
        </td>
        <td className="py-2">
          <div className="flex flex-wrap gap-1">
            {canPay && (
              <Button
                size="sm"
                variant="outline"
                aria-label={`ثبت پرداخت برای ${context}`}
                onClick={() => toggle("payment")}
              >
                ثبت پرداخت
              </Button>
            )}
            {canCancel && (
              <Button
                size="sm"
                variant="destructive"
                aria-label={`لغو ${context}`}
                onClick={() => toggle("cancel")}
              >
                لغو
              </Button>
            )}
          </div>
        </td>
      </tr>
      {action !== null && (
        <tr className="border-b bg-muted/30">
          <td colSpan={columns} className="space-y-2 py-2">
            <p className="text-xs text-muted-foreground">
              برای {context}
              {(order.memberFullName ?? order.guestName) !== null && (
                <> — {order.memberFullName ?? order.guestName}</>
              )}
            </p>
            {action === "payment" && (
              <CafeOrderPaymentForm
                orderId={order.id}
                outstanding={order.outstanding}
                onDone={() => done("پرداخت ثبت شد.")}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "cancel" && (
              <CancelCafeOrderForm
                orderId={order.id}
                netPaid={order.netPaid}
                refundWarning={isPositiveMoney(order.netPaid)}
                onDone={() =>
                  done(
                    isPositiveMoney(order.netPaid)
                      ? "سفارش لغو شد و مبلغ پرداخت‌شده بازگردانده شد."
                      : "سفارش لغو شد.",
                  )
                }
                onCancel={() => setAction(null)}
              />
            )}
          </td>
        </tr>
      )}
    </>
  );
}
