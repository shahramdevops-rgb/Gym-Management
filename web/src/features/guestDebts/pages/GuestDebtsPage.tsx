import { useState } from "react";
import { useSearchParams } from "react-router";

import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { guestLabel } from "@/features/attendance/holder";
import { CafeOrderPaymentForm } from "@/features/cafe/components/CafeOrderPaymentForm";
import { CancelCafeOrderForm } from "@/features/cafe/components/CancelCafeOrderForm";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { serviceChargeLabel } from "@/features/serviceCharges/api";
import { ServiceChargePaymentForm } from "@/features/serviceCharges/components/ServiceChargePaymentForm";
import { VoidServiceChargeForm } from "@/features/serviceCharges/components/VoidServiceChargeForm";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney, toPersianDigits } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";
import { pageFromParams } from "@/lib/searchParams";

import { useGuestDebts, type GuestDebt } from "../api";

/**
 * «بدهی مهمان‌ها» (BUSINESS_RULES.md §7 *Guest visit*, roadmap 6.5.31): everything bought on a
 * guest's visit that still owes money, cafe orders and هوازی, فروشگاه and آنالیز together, newest
 * first. Mostly what the nightly job closed unpaid, since a guest still inside settles in their
 * locker's box; such a row is marked «داخل باشگاه».
 *
 * Each row is paid, voided or cancelled with the same forms as everywhere else, so the rules are
 * the item's own: no overpayment, a reason for a void, and the money going back the way it came. A
 * row leaves the list once nothing is owed on it. The page lives in the URL (`?page=2`).
 */
export function GuestDebtsPage() {
  const [params, setParams] = useSearchParams();
  const page = pageFromParams(params);
  const debts = useGuestDebts(page);
  const [notice, setNotice] = useState<string | null>(null);

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-bold">بدهی مهمان‌ها</h2>

      <Card>
        <CardHeader>
          <CardTitle>
            خریدهای پرداخت‌نشدهٔ مهمان‌ها
            {debts.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(debts.data.totalCount)})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {notice !== null && (
            <Alert variant="success" role="status">
              {notice}
            </Alert>
          )}
          {debts.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {debts.isError && <Alert variant="destructive">{errorMessage(debts.error)}</Alert>}
          {debts.isSuccess && debts.data.items.length === 0 && (
            <p className="text-muted-foreground">بدهی پرداخت‌نشده‌ای از مهمان‌ها نمانده است.</p>
          )}
          {debts.isSuccess && debts.data.items.length > 0 && (
            <>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-muted-foreground">
                      <th className="py-2 text-start font-medium">تاریخ</th>
                      <th className="py-2 text-start font-medium">مهمان</th>
                      <th className="py-2 text-start font-medium">بابت</th>
                      <th className="py-2 text-start font-medium">مبلغ</th>
                      <th className="py-2 text-start font-medium">مانده</th>
                      <th className="py-2 text-start font-medium">عملیات</th>
                    </tr>
                  </thead>
                  <tbody>
                    {debts.data.items.map((debt) => (
                      <GuestDebtRow key={debt.id} debt={debt} onDone={setNotice} />
                    ))}
                  </tbody>
                </table>
              </div>
              <Pager
                page={page}
                pageCount={debts.data.pageCount}
                onPageChange={(next) => setParams(next > 1 ? { page: String(next) } : {})}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

const columns = 6;

type RowAction = "payment" | "undo" | null;

/** One unpaid purchase, with its payment and its void (a charge) or cancel (a cafe order). */
function GuestDebtRow({ debt, onDone }: { debt: GuestDebt; onDone: (message: string) => void }) {
  const [action, setAction] = useState<RowAction>(null);
  const isCafe = debt.target === "CafeOrder";
  const what = describe(debt);
  const paidSomething = isPositiveMoney(debt.netPaid);

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
        <td className="py-2">{formatDate(debt.day)}</td>
        <td className="py-2">
          <span className="flex flex-wrap items-center gap-1">
            <span className="font-medium">{debt.guestName}</span>
            <Badge variant="outline">{debt.visitIsOpen ? "داخل باشگاه" : guestLabel}</Badge>
          </span>
        </td>
        <td className="py-2">{what}</td>
        <td className="py-2">
          <div className="flex flex-wrap items-center gap-2">
            <span>{formatMoney(debt.amount)}</span>
            <PaymentStatusBadge status={debt.paymentStatus} />
          </div>
        </td>
        <td className="py-2 text-destructive">{formatMoney(debt.outstanding)}</td>
        <td className="py-2">
          <div className="flex flex-wrap gap-1">
            <Button
              size="sm"
              variant="outline"
              aria-label={`ثبت پرداخت برای ${what} — ${debt.guestName}`}
              onClick={() => toggle("payment")}
            >
              ثبت پرداخت
            </Button>
            <Button
              size="sm"
              variant="destructive"
              aria-label={`${isCafe ? "لغو" : "ابطال"} ${what} — ${debt.guestName}`}
              onClick={() => toggle("undo")}
            >
              {isCafe ? "لغو سفارش" : "ابطال"}
            </Button>
          </div>
        </td>
      </tr>
      {action !== null && (
        <tr className="border-b bg-muted/30">
          <td colSpan={columns} className="space-y-2 py-2">
            <p className="text-xs text-muted-foreground">
              برای {what} — {debt.guestName}
            </p>
            {action === "payment" &&
              (isCafe ? (
                <CafeOrderPaymentForm
                  orderId={debt.id}
                  outstanding={debt.outstanding}
                  onDone={() => done("پرداخت ثبت شد.")}
                  onCancel={() => setAction(null)}
                />
              ) : (
                <ServiceChargePaymentForm
                  serviceChargeId={debt.id}
                  onDone={() => done("پرداخت ثبت شد.")}
                  onCancel={() => setAction(null)}
                />
              ))}
            {action === "undo" &&
              (isCafe ? (
                <CancelCafeOrderForm
                  orderId={debt.id}
                  netPaid={debt.netPaid}
                  refundWarning={paidSomething}
                  onDone={() =>
                    done(
                      paidSomething
                        ? "سفارش لغو شد و مبلغ پرداخت‌شده بازگردانده شد."
                        : "سفارش لغو شد.",
                    )
                  }
                  onCancel={() => setAction(null)}
                />
              ) : (
                <VoidServiceChargeForm
                  serviceChargeId={debt.id}
                  refundWarning={paidSomething}
                  onDone={() =>
                    done(
                      paidSomething
                        ? "ابطال شد و مبلغ پرداخت‌شده بازگردانده شد."
                        : "ابطال شد.",
                    )
                  }
                  onCancel={() => setAction(null)}
                />
              ))}
          </td>
        </tr>
      )}
    </>
  );
}

/** «بوفه: چای × ۲»، «هوازی»، «فروشگاه: دستکش × ۳»، «آنالیز». */
function describe(debt: GuestDebt): string {
  if (debt.cafeItems !== null) {
    const lines = debt.cafeItems
      .map((item) => `${item.productName} × ${toPersianDigits(item.quantity)}`)
      .join("، ");
    return `بوفه: ${lines}`;
  }

  const label = serviceChargeLabel(debt.serviceKind, debt.description);
  const quantity = Number(debt.quantity ?? 1);

  return quantity > 1 ? `${label} × ${toPersianDigits(quantity)}` : label;
}
