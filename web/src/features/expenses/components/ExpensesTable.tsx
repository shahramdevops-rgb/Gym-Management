import { useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatDate, formatDateTime, formatMoney } from "@/lib/format";
import { cn } from "@/lib/utils";

import { useUpdateExpense, type Expense, type ExpenseCategory } from "../api";
import { ExpenseForm } from "./ExpenseForm";
import { VoidExpenseForm } from "./VoidExpenseForm";

const columns = 6;

interface ExpensesTableProps {
  expenses: Expense[];
  categories: ExpenseCategory[];
  onDone: (message: string) => void;
}

/** Expenses newest first, as the API sends them; voided ones stay in the list, marked (§9). */
export function ExpensesTable({ expenses, categories, onDone }: ExpensesTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">تاریخ</th>
            <th className="py-2 text-start font-medium">دسته‌بندی</th>
            <th className="py-2 text-start font-medium">شرح</th>
            <th className="py-2 text-start font-medium">مبلغ</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">عملیات</th>
          </tr>
        </thead>
        <tbody>
          {expenses.map((expense) => (
            <ExpenseRow
              key={expense.id}
              expense={expense}
              categories={categories}
              onDone={onDone}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

type RowAction = "edit" | "void" | null;

interface ExpenseRowProps {
  expense: Expense;
  categories: ExpenseCategory[];
  onDone: (message: string) => void;
}

/**
 * One expense and what can still be done to it. A voided expense is final (§9), so its row
 * offers nothing; a standing one can be corrected or voided, unless paying a cheque or an
 * instalment wrote it.
 */
function ExpenseRow({ expense, categories, onDone }: ExpenseRowProps) {
  const [action, setAction] = useState<RowAction>(null);
  const updateExpense = useUpdateExpense();

  const context = `هزینهٔ ${formatDate(expense.expenseDate)} به مبلغ ${formatMoney(expense.amount)}`;

  function toggle(next: Exclude<RowAction, null>) {
    setAction((current) => (current === next ? null : next));
  }

  function done(message: string) {
    setAction(null);
    onDone(message);
  }

  return (
    <>
      <tr className={cn("border-b align-top", expense.isVoided && "text-muted-foreground")}>
        <td className="py-2">{formatDate(expense.expenseDate)}</td>
        <td className="py-2">{expense.categoryName}</td>
        <td className="py-2">
          <p className="whitespace-pre-line">{expense.description}</p>
          {expense.referenceNumber !== null && (
            <p className="text-xs text-muted-foreground">مرجع: {expense.referenceNumber}</p>
          )}
        </td>
        <td className={cn("py-2", expense.isVoided && "line-through")}>
          {formatMoney(expense.amount)}
        </td>
        <td className="py-2">
          {expense.payableId !== null && <Badge variant="outline">از چک و قسط</Badge>}
          {expense.isVoided && (
            <div className="space-y-1">
              <Badge variant="destructive">باطل شده</Badge>
              <p className="text-xs">
                {formatDateTime(expense.voidedAt)} — {expense.voidReason}
              </p>
            </div>
          )}
        </td>
        <td className="py-2">
          {/* Paying a cheque or an instalment wrote it; it changes only from «چک و قسط» (§9). */}
          {!expense.isVoided && expense.payableId === null && (
            <div className="flex flex-wrap gap-1">
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
                onClick={() => toggle("void")}
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
              <ExpenseForm
                defaultValues={{
                  amount: String(expense.amount),
                  categoryId: expense.categoryId,
                  expenseDate: expense.expenseDate,
                  description: expense.description,
                  referenceNumber: expense.referenceNumber ?? "",
                }}
                categories={categories}
                submitLabel="ذخیرهٔ تغییرات"
                onSubmit={async (input) => {
                  // The version read with the list: an edit that crossed somebody else's is refused.
                  await updateExpense.mutateAsync({
                    id: expense.id,
                    version: expense.version,
                    ...input,
                  });
                  done("هزینه ویرایش شد.");
                }}
                onCancel={() => setAction(null)}
              />
            )}
            {action === "void" && (
              <VoidExpenseForm
                expenseId={expense.id}
                onDone={() => done("هزینه باطل شد.")}
                onCancel={() => setAction(null)}
              />
            )}
          </td>
        </tr>
      )}
    </>
  );
}
