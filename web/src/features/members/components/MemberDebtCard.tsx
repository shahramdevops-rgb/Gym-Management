import { CircleCheck } from "lucide-react";
import { useState } from "react";

import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { SettleDebt } from "@/features/payments/components/SettleDebt";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney, toPersianDigits } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";
import { cn } from "@/lib/utils";

import { useMemberDebt, type MemberDebtItem } from "../api";
import { debtItemLabel } from "../debtItemLabel";

/**
 * What the member owes, and what it is made of (BUSINESS_RULES.md §5 Member debt). The gym runs
 * open accounts, so a balance is ordinary rather than an alarm — but the total is never shown on
 * its own: the developer's requirement is that "جزء به جزء" is always one click away, which is
 * what the toggle below is.
 */
export function MemberDebtCard({ memberId }: { memberId: string }) {
  const debt = useMemberDebt(memberId);
  const [showItems, setShowItems] = useState(false);

  if (debt.isPending) {
    return (
      <DebtCard>
        <LoadingState />
      </DebtCard>
    );
  }

  if (debt.isError) {
    return (
      <DebtCard>
        <Alert variant="destructive">{errorMessage(debt.error)}</Alert>
      </DebtCard>
    );
  }

  const total = debt.data.total;

  // One SettleDebt in one place for both branches: its success step is on screen exactly when the
  // debt has just reached zero, and moving it would remount it and lose that step.
  return (
    <DebtCard>
      {isPositiveMoney(total) ? (
        <div className="space-y-3">
          {/* The toggle right beside the amount, not across the card at its end, where it read as
              the subscription card's button next door (the UX review, 1405/07/21). */}
          <div className="flex flex-wrap items-center gap-3">
            <p className="text-lg font-medium text-destructive">{formatMoney(total)}</p>
            <Button
              size="sm"
              variant="outline"
              aria-expanded={showItems}
              onClick={() => setShowItems((shown) => !shown)}
            >
              {showItems ? "بستن جزئیات" : "جزء به جزء"}
            </Button>
          </div>

          {showItems && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>بابت</TableHead>
                  <TableHead>تاریخ</TableHead>
                  <TableHead>مبلغ</TableHead>
                  <TableHead>پرداخت‌شده</TableHead>
                  <TableHead>مانده</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {debt.data.items.map((item) => (
                  <DebtRow key={item.id} item={item} />
                ))}
              </TableBody>
            </Table>
          )}
        </div>
      ) : (
        <EmptyState icon={CircleCheck}>این عضو بدهی ندارد.</EmptyState>
      )}

      <SettleDebt memberId={memberId} items={debt.data.items} />
    </DebtCard>
  );
}

/**
 * One owed item. A cafe order is followed by what it bought, one step in, each with its price, so
 * «بوفه» on its own never leaves the desk guessing what the money is for — the same lines as the
 * member's cafe purchases.
 */
function DebtRow({ item }: { item: MemberDebtItem }) {
  const lines = item.cafeItems;

  return (
    <>
      <TableRow
        joins={lines.length > 0 ? "below" : undefined}
        className={cn(lines.length > 0 && "border-b-0")}
      >
        {/* Edged at its start in the page's tone, like every list's rows (13.2j). */}
        <TableCell className="tone-row-edge">{debtItemLabel(item)}</TableCell>
        <TableCell label="تاریخ">{formatDate(item.startDate)}</TableCell>
        {/* An amount stays on one line: the card is half the page wide beside the subscription. */}
        <TableCell label="مبلغ" className="whitespace-nowrap">
          {formatMoney(item.price)}
        </TableCell>
        <TableCell label="پرداخت‌شده" className="whitespace-nowrap">
          {formatMoney(item.netPaid)}
        </TableCell>
        <TableCell label="مانده" className="whitespace-nowrap">
          {formatMoney(item.outstanding)}
        </TableCell>
      </TableRow>
      {lines.map((line, index) => {
        const last = index === lines.length - 1;
        return (
          <TableRow
            joins={last ? "above" : "both"}
            key={line.id}
            className={cn("text-muted-foreground", !last && "border-b-0")}
          >
            <TableCell className={cn("ps-4", last ? "pb-2" : "pb-1")}>
              {line.productName} × {toPersianDigits(line.quantity)}
            </TableCell>
            <TableCell />
            <TableCell className={cn("whitespace-nowrap", last ? "pb-2" : "pb-1")}>
              {formatMoney(line.lineTotal)}
            </TableCell>
            <TableCell />
            <TableCell />
          </TableRow>
        );
      })}
    </>
  );
}

function DebtCard({ children }: { children: React.ReactNode }) {
  return (
    <Card edge>
      <CardHeader>
        <CardTitle>بدهی</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">{children}</CardContent>
    </Card>
  );
}
