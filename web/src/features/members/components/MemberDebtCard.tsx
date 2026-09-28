import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
        <p className="text-muted-foreground">در حال بارگذاری…</p>
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
          <div className="flex flex-wrap items-center justify-between gap-3">
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
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-muted-foreground">
                    <th className="py-2 text-start font-medium">بابت</th>
                    <th className="py-2 text-start font-medium">تاریخ</th>
                    <th className="py-2 text-start font-medium">مبلغ</th>
                    <th className="py-2 text-start font-medium">پرداخت‌شده</th>
                    <th className="py-2 text-start font-medium">مانده</th>
                  </tr>
                </thead>
                <tbody>
                  {debt.data.items.map((item) => (
                    <DebtRow key={item.id} item={item} />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ) : (
        <p className="text-muted-foreground">این عضو بدهی ندارد.</p>
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
      <tr className={cn(lines.length === 0 && "border-b")}>
        <td className="py-2">{debtItemLabel(item)}</td>
        <td className="py-2">{formatDate(item.startDate)}</td>
        <td className="py-2">{formatMoney(item.price)}</td>
        <td className="py-2">{formatMoney(item.netPaid)}</td>
        <td className="py-2">{formatMoney(item.outstanding)}</td>
      </tr>
      {lines.map((line, index) => {
        const last = index === lines.length - 1;
        return (
          <tr key={line.id} className={cn("text-muted-foreground", last && "border-b")}>
            <td className={cn("ps-4", last ? "pb-2" : "pb-1")}>
              {line.productName} × {toPersianDigits(line.quantity)}
            </td>
            <td />
            <td className={last ? "pb-2" : "pb-1"}>{formatMoney(line.lineTotal)}</td>
            <td />
            <td />
          </tr>
        );
      })}
    </>
  );
}

function DebtCard({ children }: { children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>بدهی</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">{children}</CardContent>
    </Card>
  );
}
