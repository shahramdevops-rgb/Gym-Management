import type { ReactNode } from "react";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney, toPersianDigits } from "@/lib/format";

import { useMyPlan, type SitePlan } from "./api";
import { paymentStatus, planLabel, planStatus } from "./labels";

/**
 * «اشتراک من» (BUSINESS_RULES.md §15 *The member's own records*, roadmap 15.5b): the plan now as
 * three small boxes, what a member looks for first (sessions left, the last day, what is paid),
 * and under it the plan bought for later, if any. Read-only: the desk sells and renews.
 */
export function MyPlanCard() {
  const plan = useMyPlan();

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          اشتراک من
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {plan.data === undefined ? (
          plan.isError ? (
            <Alert variant="destructive">{errorMessage(plan.error)}</Alert>
          ) : (
            <LoadingState className="py-2" />
          )
        ) : plan.data.current === null ? (
          <p className="text-sm text-muted-foreground">اشتراک فعالی ندارید.</p>
        ) : (
          <CurrentPlan plan={plan.data.current} />
        )}

        {plan.data?.next != null && (
          <p className="rounded-lg border border-dashed p-3 text-sm">
            پلن بعدی: <strong className="font-semibold">{planLabel(plan.data.next)}</strong>، از{" "}
            {formatDate(plan.data.next.startDate)}
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function CurrentPlan({ plan }: { plan: SitePlan }) {
  const status = planStatus[plan.status];
  const paid = paymentStatus[plan.paymentStatus];

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="font-semibold">{planLabel(plan)}</p>
        <Badge variant={status.tone}>{status.label}</Badge>
      </div>

      <dl className="grid grid-cols-3 gap-2 text-center">
        <Box term="جلسه مانده">
          <span className="text-2xl font-bold">{toPersianDigits(plan.remainingSessions)}</span>
          <span className="text-xs text-muted-foreground">
            {" "}
            از {toPersianDigits(plan.totalSessions)}
          </span>
        </Box>
        <Box term="پایان">{formatDate(plan.endDate)}</Box>
        <Box term="پرداخت">
          <Badge variant={paid.tone}>{paid.label}</Badge>
        </Box>
      </dl>

      {plan.frozenSince !== null && (
        <p className="text-sm text-muted-foreground">از {formatDate(plan.frozenSince)} فریز است.</p>
      )}
      {plan.paymentStatus !== "Paid" && (
        <p className="text-sm text-muted-foreground">
          {formatMoney(plan.netPaid)} از {formatMoney(plan.price)} پرداخت شده است.
        </p>
      )}
    </>
  );
}

/** One small box of the three: its name above, the value under it. */
function Box({ term, children }: { term: string; children: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-1 rounded-lg border p-2">
      <dt className="text-xs text-muted-foreground">{term}</dt>
      <dd className="text-sm font-medium">{children}</dd>
    </div>
  );
}
