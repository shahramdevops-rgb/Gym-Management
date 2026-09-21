import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatMoney, formatNumber } from "@/lib/format";

import { useCurrentSubscription } from "../api";
import { AssignSubscriptionForm } from "./AssignSubscriptionForm";
import { RenewSubscriptionForm } from "./RenewSubscriptionForm";
import { SubscriptionStatusBadge } from "./SubscriptionStatusBadge";

type Panel = "assign" | "renew" | null;

interface Notice {
  kind: "success" | "destructive";
  text: string;
}

/**
 * The member's latest subscription, at a glance (BUSINESS_RULES.md §4): plan, calculated status,
 * dates, sessions left and payment status. Only selling and renewing happen here, because both
 * always create a new row with no ambiguity about which subscription they target. Every action on
 * an *existing* subscription (payment, freeze, unfreeze, cancel, refund) lives on that
 * subscription's own row in the history table below instead (task 4.6 follow-up) — a member can
 * have more than one live subscription (an active one plus a queued renewal), and this card only
 * ever shows one of them.
 */
export function CurrentSubscriptionCard({ memberId }: { memberId: string }) {
  const subscription = useCurrentSubscription(memberId);
  const [panel, setPanel] = useState<Panel>(null);
  const [notice, setNotice] = useState<Notice | null>(null);

  function togglePanel(next: Exclude<Panel, null>) {
    setNotice(null);
    setPanel((current) => (current === next ? null : next));
  }

  function done(text: string) {
    setPanel(null);
    setNotice({ kind: "success", text });
  }

  if (subscription.isPending) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>اشتراک فعلی</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">در حال بارگذاری…</p>
        </CardContent>
      </Card>
    );
  }

  if (subscription.isError) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>اشتراک فعلی</CardTitle>
        </CardHeader>
        <CardContent>
          <Alert variant="destructive">{errorMessage(subscription.error)}</Alert>
        </CardContent>
      </Card>
    );
  }

  const current = subscription.data;

  return (
    <Card>
      <CardHeader>
        <CardTitle>اشتراک فعلی</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {notice !== null && (
          <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
            {notice.text}
          </Alert>
        )}

        {current === null ? (
          <p className="text-muted-foreground">این عضو هنوز اشتراکی ندارد.</p>
        ) : (
          <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-[max-content_1fr]">
            <dt className="text-muted-foreground">پلن</dt>
            <dd>{current.planName}</dd>

            <dt className="text-muted-foreground">وضعیت</dt>
            <dd>
              <SubscriptionStatusBadge status={current.status} />
            </dd>

            <dt className="text-muted-foreground">تاریخ شروع</dt>
            <dd>{formatDate(current.startDate)}</dd>

            <dt className="text-muted-foreground">تاریخ پایان</dt>
            <dd>{formatDate(current.endDate)}</dd>

            <dt className="text-muted-foreground">جلسات باقی‌مانده</dt>
            <dd>
              {current.totalSessions === null
                ? "نامحدود"
                : formatNumber(Number(current.remainingSessions))}
            </dd>

            <dt className="text-muted-foreground">وضعیت پرداخت</dt>
            <dd className="flex flex-wrap items-center gap-2">
              <PaymentStatusBadge status={current.paymentStatus} />
              <span className="text-muted-foreground">
                {formatMoney(Number(current.netPaid))} از {formatMoney(Number(current.price))}
              </span>
            </dd>
          </dl>
        )}

        <div className="flex flex-wrap gap-2">
          <Button size="sm" onClick={() => togglePanel("assign")}>
            فروش اشتراک
          </Button>
          <Button
            size="sm"
            variant="outline"
            disabled={current === null}
            onClick={() => togglePanel("renew")}
          >
            تمدید
          </Button>
        </div>

        {panel === "assign" && (
          <AssignSubscriptionForm
            memberId={memberId}
            onDone={() => done("اشتراک فروخته شد.")}
            onCancel={() => setPanel(null)}
          />
        )}
        {panel === "renew" && current !== null && (
          <RenewSubscriptionForm
            memberId={memberId}
            planName={current.planName}
            onDone={(renewed) =>
              done(
                renewed.status === "Upcoming"
                  ? `اشتراک تمدید شد؛ از ${formatDate(renewed.startDate)} تا ${formatDate(renewed.endDate)} در انتظار شروع است.`
                  : `اشتراک تمدید شد و از همین امروز تا ${formatDate(renewed.endDate)} فعال است.`,
              )
            }
            onCancel={() => setPanel(null)}
          />
        )}
      </CardContent>
    </Card>
  );
}
