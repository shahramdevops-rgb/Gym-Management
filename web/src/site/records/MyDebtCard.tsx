import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney } from "@/lib/format";

import { useMyDebt, type MyDebtItem } from "./api";
import { cafeItemsLine, cafeLabel, planLabel, serviceLabel } from "./labels";

/**
 * «بدهی من» (BUSINESS_RULES.md §5 *Member debt*, §15 *The member's own records*): the total, then
 * each thing still owed for, so the member knows what the desk will ask for. Nothing owed is one
 * line, not an empty list.
 */
export function MyDebtCard() {
  const debt = useMyDebt();

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          بدهی من
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {debt.data === undefined ? (
          debt.isError ? (
            <Alert variant="destructive">{errorMessage(debt.error)}</Alert>
          ) : (
            <LoadingState className="py-2" />
          )
        ) : debt.data.items.length === 0 ? (
          <p className="text-sm text-muted-foreground">بدهی ندارید.</p>
        ) : (
          <>
            <p className="flex items-baseline justify-between gap-2">
              <span className="text-sm text-muted-foreground">جمع بدهی</span>
              <strong className="text-lg font-bold text-destructive">
                {formatMoney(debt.data.total)}
              </strong>
            </p>
            <ul aria-label="بدهی من" className="divide-y">
              {debt.data.items.map((item) => (
                <li
                  key={`${item.kind}-${item.id}`}
                  className="flex items-start justify-between gap-3 py-2 first:pt-0 last:pb-0"
                >
                  <div className="min-w-0 space-y-0.5">
                    <p className="font-medium">{debtItemLabel(item)}</p>
                    <p className="text-sm text-muted-foreground">
                      {formatDate(item.startDate)}
                      {item.cafeItems.length > 0 && ` · ${cafeItemsLine(item.cafeItems)}`}
                    </p>
                  </div>
                  <span className="shrink-0 text-sm font-medium">
                    {formatMoney(item.outstanding)}
                  </span>
                </li>
              ))}
            </ul>
          </>
        )}
      </CardContent>
    </Card>
  );
}

/** What a debt row is for, as the panel's debt card says it («اشتراک ۱۲ جلسه - ۳۰ روزه»). */
function debtItemLabel(item: MyDebtItem): string {
  switch (item.kind) {
    case "ServiceCharge":
      return serviceLabel(item.serviceKind, item.sale?.description);
    case "CafeOrder":
      return cafeLabel;
    case "Subscription":
      return item.plan === null ? "اشتراک" : `اشتراک ${planLabel(item.plan)}`;
  }
}
