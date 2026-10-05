import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { ExpenseCategory } from "@/features/expenses/api";
import { formatDate, formatDateTime, formatMoney, gymToday, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import {
  kindText,
  paidLabel,
  payableKindLabels,
  payableStatusLabels,
  useUpdatePayable,
  type Payable,
} from "../api";
import { canPayToday } from "../schemas";
import { PayableForm } from "./PayableForm";
import { PayConfirm } from "./PayConfirm";
import { CancelPayableForm, RevertPayableForm } from "./ReasonForm";

const columns = 7;

interface PayablesTableProps {
  payables: Payable[];
  categories: ExpenseCategory[];
  onDone: (message: string) => void;
}

/** In the API's order; paid and cancelled ones stay in the register, marked (§9). */
export function PayablesTable({ payables, categories, onDone }: PayablesTableProps) {
  const today = gymToday();

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">نوع</th>
            <th className="py-2 text-start font-medium">تاریخ</th>
            <th className="py-2 text-start font-medium">گیرنده</th>
            <th className="py-2 text-start font-medium">شرح</th>
            <th className="py-2 text-start font-medium">مبلغ</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">عملیات</th>
          </tr>
        </thead>
        <tbody>
          {payables.map((payable) => (
            <PayableRow
              key={payable.id}
              payable={payable}
              categories={categories}
              today={today}
              onDone={onDone}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

type RowAction = "edit" | "pay" | "revert" | "cancel" | null;

interface PayableRowProps {
  payable: Payable;
  categories: ExpenseCategory[];
  today: string;
  onDone: (message: string) => void;
}

/**
 * One cheque or instalment and what can still be done to it. A pending one can be corrected,
 * cancelled, or paid (a cheque once its date has come, an instalment any time); a paid one can go
 * back to pending; a cancelled one is final (§9).
 */
function PayableRow({ payable, categories, today, onDone }: PayableRowProps) {
  const [action, setAction] = useState<RowAction>(null);
  const updatePayable = useUpdatePayable();

  const pending = payable.status === "Pending";
  const canPay = canPayToday(payable.kind, payable.dueDate, today);
  const overdue = pending && payable.dueDate < today;
  const name = payableKindLabels[payable.kind];
  const context = `${kindText(payable)} ${formatDate(payable.dueDate)} به ${payable.payee} به مبلغ ${formatMoney(payable.amount)}`;

  function toggle(next: Exclude<RowAction, null>) {
    setAction((current) => (current === next ? null : next));
  }

  function done(message: string) {
    setAction(null);
    onDone(message);
  }

  return (
    <>
      <tr className={cn("border-b align-top", !pending && "text-muted-foreground")}>
        <td className="py-2">{kindText(payable)}</td>
        <td className="py-2">{formatDate(payable.dueDate)}</td>
        <td className="py-2">{payable.payee}</td>
        <td className="py-2">
          <p className="whitespace-pre-line">{payable.description}</p>
          <p className="text-xs text-muted-foreground">{payable.categoryName}</p>
        </td>
        <td className={cn("py-2", payable.status === "Cancelled" && "line-through")}>
          {formatMoney(payable.amount)}
        </td>
        <td className="py-2">
          <div className="space-y-1">
            {payable.status === "Pending" && (
              <Badge variant={overdue ? "destructive" : "outline"}>
                {overdue ? "سررسید گذشته" : payableStatusLabels.Pending}
              </Badge>
            )}
            {payable.status === "Paid" && (
              <>
                <Badge variant="success">{paidLabel(payable.kind)}</Badge>
                <p className="text-xs">{formatDateTime(payable.paidAt)}</p>
                <Link to={paths.expenses} className="text-xs underline">
                  در هزینه‌ها ثبت شد
                </Link>
              </>
            )}
            {payable.status === "Cancelled" && (
              <>
                <Badge variant="destructive">{payableStatusLabels.Cancelled}</Badge>
                <p className="text-xs">
                  {formatDateTime(payable.cancelledAt)} — {payable.cancelReason}
                </p>
              </>
            )}
          </div>
        </td>
        <td className="py-2">
          {pending && (
            <div className="flex flex-wrap gap-1">
              {canPay && (
                <Button
                  size="sm"
                  aria-label={`${paidLabel(payable.kind)}، ${context}`}
                  onClick={() => toggle("pay")}
                >
                  {paidLabel(payable.kind)}
                </Button>
              )}
              <Button
                size="sm"
                variant="outline"
                aria-label={`ویرایش ${context}`}
                onClick={() => toggle("edit")}
              >
                ویرایش
              </Button>
              <Button
                size="sm"
                variant="destructive"
                aria-label={`ابطال ${context}`}
                onClick={() => toggle("cancel")}
              >
                ابطال
              </Button>
            </div>
          )}
          {payable.status === "Paid" && (
            <Button
              size="sm"
              variant="outline"
              aria-label={`برگشت به در انتظار، ${context}`}
              onClick={() => toggle("revert")}
            >
              برگشت به در انتظار
            </Button>
          )}
        </td>
      </tr>
      {action !== null && (
        <tr className="border-b bg-muted/30">
          <td colSpan={columns} className="space-y-2 py-2">
            <p className="text-xs text-muted-foreground">برای {context}</p>
            {action === "edit" && (
              <PayableForm
                defaultValues={{
                  kind: payable.kind,
                  amount: String(payable.amount),
                  dueDate: payable.dueDate,
                  payee: payable.payee,
                  description: payable.description,
                  categoryId: payable.categoryId,
                  installmentNumber:
                    payable.installmentNumber == null
                      ? ""
                      : toPersianDigits(payable.installmentNumber),
                  installmentCount:
                    payable.installmentCount == null
                      ? ""
                      : toPersianDigits(payable.installmentCount),
                }}
                categories={categories}
                submitLabel="ذخیرهٔ تغییرات"
                onSubmit={async (input) => {
                  // The version read with the list: an edit that crossed somebody else's is refused.
                  await updatePayable.mutateAsync({
                    id: payable.id,
                    version: payable.version,
                    ...input,
                  });
                  done(`${payableKindLabels[input.kind]} ویرایش شد.`);
                }}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "pay" && (
              <PayConfirm
                payableId={payable.id}
                kind={payable.kind}
                onDone={() => done(`${name} ${paidLabel(payable.kind)} و در هزینه‌ها ثبت شد.`)}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "revert" && (
              <RevertPayableForm
                payableId={payable.id}
                onDone={() => done(`${name} به در انتظار برگشت و هزینه‌اش باطل شد.`)}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "cancel" && (
              <CancelPayableForm
                payableId={payable.id}
                onDone={() => done(`${name} باطل شد.`)}
                onCancel={() => setAction(null)}
              />
            )}
          </td>
        </tr>
      )}
    </>
  );
}
