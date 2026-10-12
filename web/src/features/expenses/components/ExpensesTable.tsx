import { useState } from "react";

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
import { stateEdge } from "@/lib/edges";
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

/**
 * Expenses newest first, as the API sends them; voided ones stay in the list, marked (§9). Drawn
 * like the cafe's orders (13.2i): each row edged at its start in the page's tone, its tags and
 * buttons edged at their end in a state's colour (`stateEdge`): red for a voided expense and for
 * «ابطال», grey for one that «چک و قسط» wrote.
 */
export function ExpensesTable({ expenses, categories, onDone }: ExpensesTableProps) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>تاریخ</TableHead>
          <TableHead>دسته‌بندی</TableHead>
          <TableHead>شرح</TableHead>
          <TableHead>مبلغ</TableHead>
          <TableHead>وضعیت</TableHead>
          <TableHead>عملیات</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {expenses.map((expense) => (
          <ExpenseRow key={expense.id} expense={expense} categories={categories} onDone={onDone} />
        ))}
      </TableBody>
    </Table>
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
      <TableRow
        joins={action !== null ? "below" : undefined}
        className={cn("align-top", expense.isVoided && "text-muted-foreground")}
      >
        {/* Edged at its start in the page's tone, like a locker door (`tone-row-edge`, 13.2i). */}
        <TableCell className="tone-row-edge">{formatDate(expense.expenseDate)}</TableCell>
        <TableCell label="دسته‌بندی">{expense.categoryName}</TableCell>
        <TableCell label="شرح">
          <p className="whitespace-pre-line">{expense.description}</p>
          {expense.referenceNumber !== null && (
            <p className="text-xs text-muted-foreground">مرجع: {expense.referenceNumber}</p>
          )}
        </TableCell>
        <TableCell label="مبلغ" className={cn("", expense.isVoided && "line-through")}>
          {formatMoney(expense.amount)}
        </TableCell>
        <TableCell label="وضعیت">
          {expense.payableId !== null && (
            <Badge variant="ghost" className={stateEdge.muted}>
              از چک و قسط
            </Badge>
          )}
          {expense.isVoided && (
            <div className="space-y-1">
              <Badge variant="ghost" className={cn(stateEdge.destructive, "font-semibold")}>
                باطل شده
              </Badge>
              <p className="text-xs">
                {formatDateTime(expense.voidedAt)} — {expense.voidReason}
              </p>
            </div>
          )}
        </TableCell>
        <TableCell>
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
                variant="ghost"
                className={cn(
                  stateEdge.destructive,
                  "hover:bg-destructive/10 hover:text-destructive",
                )}
                aria-label={`ابطال ${context}`}
                onClick={() => toggle("void")}
              >
                ابطال
              </Button>
            </div>
          )}
        </TableCell>
      </TableRow>
      {action !== null && (
        <TableRow joins="above" className="bg-muted/30">
          <TableCell colSpan={columns} className="space-y-2 py-2">
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
                editing
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
          </TableCell>
        </TableRow>
      )}
    </>
  );
}
