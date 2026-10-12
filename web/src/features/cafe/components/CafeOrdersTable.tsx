import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { guestLabel } from "@/features/attendance/holder";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { stateEdge } from "@/lib/edges";
import { formatDate, formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";
import { cn } from "@/lib/utils";

import type { CafeOrder } from "../api";
import { CafeOrderPaymentForm } from "./CafeOrderPaymentForm";
import { CancelCafeOrderForm } from "./CancelCafeOrderForm";

interface CafeOrdersTableProps {
  orders: CafeOrder[];
  /** False on a member's own profile, where every row is the same person. */
  showCustomer?: boolean;
  onDone: (message: string) => void;
}

/**
 * Orders newest first, as the API sends them; cancelled ones stay in the list, marked (§8). Drawn
 * like the history's lists (13.2i): each row edged at its start in the page's tone, its tags and
 * buttons edged at their end in a state's colour (`stateEdge`): green to pay, red for what is
 * owed, cancelled or undone, grey for a guest who owes nothing.
 */
export function CafeOrdersTable({ orders, showCustomer = true, onDone }: CafeOrdersTableProps) {
  const columns = showCustomer ? 7 : 6;

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>تاریخ</TableHead>
          {showCustomer && <TableHead>مشتری</TableHead>}
          <TableHead>اقلام</TableHead>
          <TableHead>مبلغ</TableHead>
          <TableHead>پرداخت</TableHead>
          <TableHead>وضعیت</TableHead>
          <TableHead>عملیات</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {orders.map((order) => (
          <CafeOrderRow
            key={order.id}
            order={order}
            showCustomer={showCustomer}
            columns={columns}
            onDone={onDone}
          />
        ))}
      </TableBody>
    </Table>
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
      <TableRow joins={action !== null ? "below" : undefined} className="align-top">
        {/* Edged at its start in the page's tone, like a locker door (`tone-row-edge`, 13.2i). */}
        <TableCell className="tone-row-edge">{formatDate(order.orderedOn)}</TableCell>
        {showCustomer && (
          <TableCell label="مشتری">
            {order.guestName !== null ? (
              // An order on a guest's visit (BUSINESS_RULES.md §7 *Guest visit*): their name, no profile.
              <span className="flex flex-wrap items-center gap-1">
                {order.guestName}
                {!cancelled && isPositiveMoney(order.outstanding) ? (
                  <Badge variant="ghost" className={cn(stateEdge.destructive, "font-semibold")}>
                    پرداخت‌نشده — مهمان
                  </Badge>
                ) : (
                  <Badge variant="ghost" className={stateEdge.muted}>
                    {guestLabel}
                  </Badge>
                )}
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
          </TableCell>
        )}
        <TableCell label="اقلام">
          <ul>
            {order.items.map((item) => (
              <li key={item.id}>
                {item.productName} × {toPersianDigits(item.quantity)}
              </li>
            ))}
          </ul>
        </TableCell>
        <TableCell label="مبلغ">{formatMoney(order.totalAmount)}</TableCell>
        <TableCell label="پرداخت">
          <div className="flex flex-wrap items-center gap-2">
            {!cancelled && <PaymentStatusBadge status={order.paymentStatus} edged />}
            <span className="text-muted-foreground">{formatMoney(order.netPaid)}</span>
          </div>
        </TableCell>
        <TableCell label="وضعیت">
          {cancelled ? (
            <div className="space-y-1">
              <Badge variant="ghost" className={cn(stateEdge.destructive, "font-semibold")}>
                لغو شده
              </Badge>
              <p className="text-xs text-muted-foreground">
                {formatDateTime(order.cancelledAt)} — {order.cancelReason}
              </p>
            </div>
          ) : (
            isPositiveMoney(order.outstanding) && (
              <span className="text-destructive">مانده {formatMoney(order.outstanding)}</span>
            )
          )}
        </TableCell>
        <TableCell>
          <div className="flex flex-wrap gap-1">
            {canPay && (
              <Button
                size="sm"
                variant="ghost"
                className={cn(stateEdge.success, "hover:bg-success/10 hover:text-success-ink")}
                aria-label={`ثبت پرداخت برای ${context}`}
                onClick={() => toggle("payment")}
              >
                ثبت پرداخت
              </Button>
            )}
            {canCancel && (
              <Button
                size="sm"
                variant="ghost"
                className={cn(
                  stateEdge.destructive,
                  "hover:bg-destructive/10 hover:text-destructive",
                )}
                aria-label={`لغو ${context}`}
                onClick={() => toggle("cancel")}
              >
                لغو
              </Button>
            )}
          </div>
        </TableCell>
      </TableRow>
      {action !== null && (
        <TableRow joins="above" className="bg-muted/30">
          <TableCell colSpan={columns} className="space-y-2 py-2">
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
          </TableCell>
        </TableRow>
      )}
    </>
  );
}
