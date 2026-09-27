import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { usePrices } from "@/features/settings/api";
import { errorMessage } from "@/lib/errors";
import { formatMoney } from "@/lib/format";

interface SingleVisitOfferProps {
  memberId: string;
  onSell: () => void;
  selling: boolean;
}

/**
 * What the desk is offered when someone cannot come in today (roadmap 6.5.4): one primary action —
 * sell a single visit and let them in — with selling a plan beside it. It sits in the check-in box
 * under the refusal's reason (BUSINESS_RULES.md §7 *Confirming at the front desk*).
 *
 * It appears only after check-in has refused, never before. That is what stops a member who can
 * already come in from being charged for a visit they do not need: the API is the one that decides
 * whether they can, and this offer exists only because it said no (BUSINESS_RULES.md §4). The
 * alternative — working the answer out from the member row before anyone clicks — would mean a
 * second copy of "usable today" living in the browser, and it is the copy that would be wrong.
 *
 * The price on the button is the Owner's single-visit price (BUSINESS_RULES.md §3 *Prices*); the
 * desk never types it. While the Owner has not set it, the offer says so instead of offering a
 * button that would fail with nothing to explain it.
 */
export function SingleVisitOffer({ memberId, onSell, selling }: SingleVisitOfferProps) {
  const prices = usePrices();
  const price = prices.data?.singleVisitPrice ?? null;

  return (
    <div className="space-y-3">
      {prices.isPending && <p className="text-sm text-muted-foreground">در حال بررسی…</p>}

      {prices.isError && <Alert variant="destructive">{errorMessage(prices.error)}</Alert>}

      {prices.isSuccess && price === null && (
        <Alert>{errorMessage({ code: "Pricing.SingleVisitPriceNotSet" })}</Alert>
      )}

      <div className="flex flex-wrap gap-2">
        {price !== null && (
          <Button disabled={selling} onClick={onSell}>
            {selling ? "در حال ثبت…" : `ورود تک‌جلسه‌ای — ${formatMoney(price)}`}
          </Button>
        )}
        <Button asChild variant="outline">
          <Link to={paths.member(memberId)}>فروش اشتراک</Link>
        </Button>
      </div>
    </div>
  );
}
