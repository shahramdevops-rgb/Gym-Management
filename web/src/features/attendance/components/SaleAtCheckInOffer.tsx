import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { PlanForm, type PlanChoice } from "@/features/subscriptions/components/PlanForm";
import { usePrices } from "@/features/settings/api";
import { errorMessage } from "@/lib/errors";
import { formatMoney } from "@/lib/format";

interface SaleAtCheckInOfferProps {
  memberId: string;
  /** Whether a plan sold now would start today (`canSellPlanForToday`). */
  planHere: boolean;
  /** Selling a single visit is in flight. */
  selling: boolean;
  onSellSingleVisit: () => void;
  /** Sells the plan and checks the member in; a rejection is shown in the plan form. */
  onSellPlan: (plan: PlanChoice) => Promise<void>;
}

/**
 * What the desk is offered when someone cannot come in today (roadmap 6.5.4, 6.5.7): a single
 * visit, or a plan — both sold right here and checked in with the locker the desk already clicked,
 * so the locker is chosen once. Payment is not asked for: what is owed is on the visit's box, where
 * the desk can collect it now or later (BUSINESS_RULES.md §5).
 *
 * It appears only after check-in has refused, never before. That is what stops a member who can
 * already come in from being charged for something they do not need: the API is the one that
 * decides whether they can, and this offer exists only because it said no (BUSINESS_RULES.md §4).
 *
 * When a new plan could not start today (the member has a frozen plan, or one bought for later),
 * selling a plan is left to the profile, as before: here it would only be refused.
 *
 * The prices are the Owner's (BUSINESS_RULES.md §3 *Prices*); the desk never types one. While the
 * Owner has not set the single-visit price, the offer says so instead of offering a button that
 * would fail with nothing to explain it.
 */
export function SaleAtCheckInOffer({
  memberId,
  planHere,
  selling,
  onSellSingleVisit,
  onSellPlan,
}: SaleAtCheckInOfferProps) {
  const prices = usePrices();
  const price = prices.data?.singleVisitPrice ?? null;
  const [planOpen, setPlanOpen] = useState(false);

  return (
    <div className="space-y-3">
      {prices.isPending && <p className="text-sm text-muted-foreground">در حال بررسی…</p>}

      {prices.isError && <Alert variant="destructive">{errorMessage(prices.error)}</Alert>}

      {prices.isSuccess && price === null && (
        <Alert>{errorMessage({ code: "Pricing.SingleVisitPriceNotSet" })}</Alert>
      )}

      <div className="flex flex-wrap gap-2">
        {price !== null && (
          <Button disabled={selling} onClick={onSellSingleVisit}>
            {selling ? "در حال ثبت…" : `ورود تک‌جلسه‌ای — ${formatMoney(price)}`}
          </Button>
        )}
        {planHere ? (
          <Button
            variant="outline"
            aria-expanded={planOpen}
            onClick={() => setPlanOpen((open) => !open)}
          >
            فروش اشتراک
          </Button>
        ) : (
          <Button asChild variant="outline">
            <Link to={paths.member(memberId)}>فروش اشتراک</Link>
          </Button>
        )}
      </div>

      {planHere && planOpen && (
        <section aria-label="فروش اشتراک" className="rounded-lg border p-3">
          <PlanForm
            submitLabel="فروش و ثبت ورود"
            onSubmit={onSellPlan}
            onCancel={() => setPlanOpen(false)}
          />
        </section>
      )}
    </div>
  );
}
