import { useState, type ReactNode } from "react";

import { Pager } from "@/components/Pager";
import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { choiceClass } from "@/lib/edges";
import { errorMessage } from "@/lib/errors";

import { useMyCafeOrders, useMyPayments, useMyPlans, useMyVisits } from "./api";
import { CafeOrderRow, PaymentRow, PlanRow, VisitRow } from "./HistoryRows";

type Tab = "visits" | "plans" | "payments" | "cafe";

const tabs: { value: Tab; label: string }[] = [
  { value: "visits", label: "مراجعه‌ها" },
  { value: "plans", label: "اشتراک‌ها" },
  { value: "payments", label: "پرداخت‌ها" },
  { value: "cafe", label: "بوفه" },
];

/**
 * «سوابق من» (BUSINESS_RULES.md §15 *The member's own records*): every check-in, plan, payment
 * and cafe purchase, one tab each, ten rows a page. A tab is read only once it is opened, and
 * each keeps its page while the member looks at another. A button group with `role="tablist"`,
 * as the panel's profile has, in the site's own copy.
 */
export function MyHistoryCard() {
  const [tab, setTab] = useState<Tab>("visits");
  const [pages, setPages] = useState<Record<Tab, number>>({
    visits: 1,
    plans: 1,
    payments: 1,
    cafe: 1,
  });

  const visits = useMyVisits(pages.visits, tab === "visits");
  const plans = useMyPlans(pages.plans, tab === "plans");
  const payments = useMyPayments(pages.payments, tab === "payments");
  const cafeOrders = useMyCafeOrders(pages.cafe, tab === "cafe");

  const pager = (pageCount: number) => (
    <Pager
      page={pages[tab]}
      pageCount={pageCount}
      onPageChange={(page) => setPages((current) => ({ ...current, [tab]: page }))}
    />
  );

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          سوابق من
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div role="tablist" aria-label="سوابق من" className="grid grid-cols-4 gap-1">
          {tabs.map((item) => (
            <Button
              key={item.value}
              role="tab"
              aria-selected={tab === item.value}
              size="sm"
              variant="ghost"
              className={`px-1 ${choiceClass(tab === item.value)}`}
              onClick={() => setTab(item.value)}
            >
              {item.label}
            </Button>
          ))}
        </div>

        <div role="tabpanel" aria-label={tabs.find((item) => item.value === tab)?.label}>
          {tab === "visits" && (
            <History
              query={visits}
              empty="هنوز مراجعه‌ای ثبت نشده است."
              pager={pager}
              row={(visit) => <VisitRow key={visit.id} visit={visit} />}
            />
          )}
          {tab === "plans" && (
            <History
              query={plans}
              empty="هنوز اشتراکی نداشته‌اید."
              pager={pager}
              row={(plan) => <PlanRow key={plan.id} plan={plan} />}
            />
          )}
          {tab === "payments" && (
            <History
              query={payments}
              empty="هنوز پرداختی ثبت نشده است."
              pager={pager}
              row={(payment) => <PaymentRow key={payment.id} payment={payment} />}
            />
          )}
          {tab === "cafe" && (
            <History
              query={cafeOrders}
              empty="هنوز از بوفه خریدی نداشته‌اید."
              pager={pager}
              row={(order) => <CafeOrderRow key={order.id} order={order} />}
            />
          )}
        </div>
      </CardContent>
    </Card>
  );
}

interface HistoryQuery<T> {
  data: { items: T[]; pageCount: number } | undefined;
  isError: boolean;
  error: unknown;
}

/** One tab's list: loading, refused, empty, or its rows and the pager. */
function History<T>({
  query,
  empty,
  pager,
  row,
}: {
  query: HistoryQuery<T>;
  empty: string;
  pager: (pageCount: number) => ReactNode;
  row: (item: T) => ReactNode;
}) {
  if (query.data === undefined) {
    return query.isError ? (
      <Alert variant="destructive">{errorMessage(query.error)}</Alert>
    ) : (
      <LoadingState className="py-2" />
    );
  }

  if (query.data.items.length === 0) {
    return <p className="text-sm text-muted-foreground">{empty}</p>;
  }

  return (
    <div className="space-y-4">
      <ul className="divide-y">{query.data.items.map(row)}</ul>
      {pager(query.data.pageCount)}
    </div>
  );
}
