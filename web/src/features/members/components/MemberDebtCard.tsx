import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";

import { useMemberDebt } from "../api";

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

  if (!isPositiveMoney(total)) {
    return (
      <DebtCard>
        <p className="text-muted-foreground">این عضو بدهی ندارد.</p>
      </DebtCard>
    );
  }

  return (
    <DebtCard>
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
                <tr key={item.subscriptionId} className="border-b">
                  <td className="py-2">اشتراک {item.planName}</td>
                  <td className="py-2">{formatDate(item.startDate)}</td>
                  <td className="py-2">{formatMoney(item.price)}</td>
                  <td className="py-2">{formatMoney(item.netPaid)}</td>
                  <td className="py-2">{formatMoney(item.outstanding)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </DebtCard>
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
