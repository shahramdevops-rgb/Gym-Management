import { useState } from "react";

import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { hasRole, useCurrentUser } from "@/features/auth/api";
import { useMemberPayments } from "@/features/payments/api";
import { PaymentHistoryTable } from "@/features/payments/components/PaymentHistoryTable";
import { useMemberSubscriptions } from "@/features/subscriptions/api";
import { SubscriptionHistoryTable } from "@/features/subscriptions/components/SubscriptionHistoryTable";
import { errorMessage } from "@/lib/errors";

type Tab = "subscriptions" | "payments";

const tabs: { value: Tab; label: string }[] = [
  { value: "subscriptions", label: "اشتراک‌ها" },
  { value: "payments", label: "پرداخت‌ها" },
];

/**
 * Subscription and payment history, as two tabs (task 4.6). A manual button group with
 * `role="tablist"`, the same pattern PlansPage already uses for its status filter, rather than a
 * new Radix Tabs component this codebase has not needed before.
 */
export function MemberHistoryTabs({ memberId }: { memberId: string }) {
  const currentUser = useCurrentUser();
  const isOwner = hasRole(currentUser.data, "Owner");
  const [tab, setTab] = useState<Tab>("subscriptions");
  const [subscriptionsPage, setSubscriptionsPage] = useState(1);
  const [paymentsPage, setPaymentsPage] = useState(1);
  const [notice, setNotice] = useState<string | null>(null);

  const subscriptions = useMemberSubscriptions(memberId, subscriptionsPage, {
    enabled: tab === "subscriptions",
  });
  const payments = useMemberPayments(memberId, paymentsPage, { enabled: tab === "payments" });

  return (
    <Card>
      <CardContent className="space-y-4">
        <div role="tablist" aria-label="تاریخچه عضو" className="flex gap-1 border-b">
          {tabs.map((item) => (
            <Button
              key={item.value}
              role="tab"
              aria-selected={tab === item.value}
              size="sm"
              variant={tab === item.value ? "secondary" : "ghost"}
              onClick={() => setTab(item.value)}
            >
              {item.label}
            </Button>
          ))}
        </div>

        {tab === "subscriptions" && (
          <div className="space-y-4">
            {notice !== null && (
              <Alert variant="success" role="status">
                {notice}
              </Alert>
            )}
            {subscriptions.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
            {subscriptions.isError && <Alert variant="destructive">{errorMessage(subscriptions.error)}</Alert>}
            {subscriptions.isSuccess && subscriptions.data.items.length === 0 && (
              <p className="text-muted-foreground">این عضو هنوز اشتراکی نداشته است.</p>
            )}
            {subscriptions.isSuccess && subscriptions.data.items.length > 0 && (
              <>
                <SubscriptionHistoryTable
                  subscriptions={subscriptions.data.items}
                  isOwner={isOwner}
                  onActionDone={setNotice}
                />
                <Pager
                  page={subscriptionsPage}
                  pageCount={subscriptions.data.pageCount}
                  onPageChange={setSubscriptionsPage}
                />
              </>
            )}
          </div>
        )}

        {tab === "payments" && (
          <div className="space-y-4">
            {payments.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
            {payments.isError && <Alert variant="destructive">{errorMessage(payments.error)}</Alert>}
            {payments.isSuccess && payments.data.items.length === 0 && (
              <p className="text-muted-foreground">هنوز پرداختی برای این عضو ثبت نشده است.</p>
            )}
            {payments.isSuccess && payments.data.items.length > 0 && (
              <>
                <PaymentHistoryTable payments={payments.data.items} />
                <Pager page={paymentsPage} pageCount={payments.data.pageCount} onPageChange={setPaymentsPage} />
              </>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
