import { useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatDate, formatDateTime, formatMoney, gymToday } from "@/lib/format";
import { cn } from "@/lib/utils";

import { chequeStatusLabels, useUpdateCheque, type Cheque } from "../api";
import { isDue } from "../schemas";
import { CancelChequeForm } from "./CancelChequeForm";
import { ChequeForm } from "./ChequeForm";
import { PassChequeConfirm } from "./PassChequeConfirm";

const columns = 6;

interface ChequesTableProps {
  cheques: Cheque[];
  onDone: (message: string) => void;
}

/** The cheques in the API's order; passed and cancelled ones stay in the register, marked (§9). */
export function ChequesTable({ cheques, onDone }: ChequesTableProps) {
  const today = gymToday();

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">تاریخ چک</th>
            <th className="py-2 text-start font-medium">در وجه</th>
            <th className="py-2 text-start font-medium">شرح</th>
            <th className="py-2 text-start font-medium">مبلغ</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">عملیات</th>
          </tr>
        </thead>
        <tbody>
          {cheques.map((cheque) => (
            <ChequeRow key={cheque.id} cheque={cheque} today={today} onDone={onDone} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

type RowAction = "edit" | "pass" | "cancel" | null;

interface ChequeRowProps {
  cheque: Cheque;
  today: string;
  onDone: (message: string) => void;
}

/**
 * One cheque and what can still be done to it. Passed and cancelled are final (§9), so their rows
 * offer nothing; a pending one can be corrected or cancelled, and passed once its date has come.
 */
function ChequeRow({ cheque, today, onDone }: ChequeRowProps) {
  const [action, setAction] = useState<RowAction>(null);
  const updateCheque = useUpdateCheque();

  const pending = cheque.status === "Pending";
  const due = isDue(cheque.dueDate, today);
  const overdue = pending && cheque.dueDate < today;
  const context = `چک ${formatDate(cheque.dueDate)} در وجه ${cheque.payee} به مبلغ ${formatMoney(cheque.amount)}`;

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
        <td className="py-2">{formatDate(cheque.dueDate)}</td>
        <td className="py-2">{cheque.payee}</td>
        <td className="py-2">
          <p className="whitespace-pre-line">{cheque.description}</p>
        </td>
        <td className={cn("py-2", cheque.status === "Cancelled" && "line-through")}>
          {formatMoney(cheque.amount)}
        </td>
        <td className="py-2">
          <div className="space-y-1">
            {cheque.status === "Pending" && (
              <Badge variant={overdue ? "destructive" : "outline"}>
                {overdue ? "سررسید گذشته" : chequeStatusLabels.Pending}
              </Badge>
            )}
            {cheque.status === "Passed" && (
              <>
                <Badge variant="success">{chequeStatusLabels.Passed}</Badge>
                <p className="text-xs">{formatDateTime(cheque.passedAt)}</p>
              </>
            )}
            {cheque.status === "Cancelled" && (
              <>
                <Badge variant="destructive">{chequeStatusLabels.Cancelled}</Badge>
                <p className="text-xs">
                  {formatDateTime(cheque.cancelledAt)} — {cheque.cancelReason}
                </p>
              </>
            )}
          </div>
        </td>
        <td className="py-2">
          {pending && (
            <div className="flex flex-wrap gap-1">
              {due && (
                <Button size="sm" aria-label={`پاس شد، ${context}`} onClick={() => toggle("pass")}>
                  پاس شد
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
        </td>
      </tr>
      {action !== null && (
        <tr className="border-b bg-muted/30">
          <td colSpan={columns} className="space-y-2 py-2">
            <p className="text-xs text-muted-foreground">برای {context}</p>
            {action === "edit" && (
              <ChequeForm
                defaultValues={{
                  amount: String(cheque.amount),
                  dueDate: cheque.dueDate,
                  payee: cheque.payee,
                  description: cheque.description,
                }}
                submitLabel="ذخیرهٔ تغییرات"
                onSubmit={async (input) => {
                  // The version read with the list: an edit that crossed somebody else's is refused.
                  await updateCheque.mutateAsync({
                    id: cheque.id,
                    version: cheque.version,
                    ...input,
                  });
                  done("چک ویرایش شد.");
                }}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "pass" && (
              <PassChequeConfirm
                chequeId={cheque.id}
                onDone={() => done("چک پاس شد. هزینهٔ آن را در هزینه‌ها ثبت کنید.")}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "cancel" && (
              <CancelChequeForm
                chequeId={cheque.id}
                onDone={() => done("چک باطل شد.")}
                onCancel={() => setAction(null)}
              />
            )}
          </td>
        </tr>
      )}
    </>
  );
}
