import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { useSingleSessionPlan } from "@/features/plans/api";
import { errorMessage } from "@/lib/errors";
import { formatMoney } from "@/lib/format";

interface SingleVisitOfferProps {
  memberId: string;
  onSell: (planId: string) => void;
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
 * When the single-session plan is missing or switched off, the offer says so instead of offering
 * a button that would fail with nothing to explain it (BUSINESS_RULES.md §3).
 */
export function SingleVisitOffer({ memberId, onSell, selling }: SingleVisitOfferProps) {
  const plan = useSingleSessionPlan();
  const sellable = plan.isSuccess && plan.data !== null && plan.data.isActive ? plan.data : null;

  return (
    <div className="space-y-3">
      {plan.isPending && <p className="text-sm text-muted-foreground">در حال بررسی…</p>}

      {plan.isError && <Alert variant="destructive">{errorMessage(plan.error)}</Alert>}

      {plan.isSuccess && plan.data === null && (
        <Alert>
          هنوز پلن تک‌جلسه‌ای ساخته نشده است. مدیر باید آن را یک بار در صفحهٔ پلن‌ها بسازد.
        </Alert>
      )}

      {plan.isSuccess && plan.data !== null && !plan.data.isActive && (
        <Alert>پلن تک‌جلسه‌ای غیرفعال است، پس فروش ورود تک‌جلسه‌ای ممکن نیست.</Alert>
      )}

      <div className="flex flex-wrap gap-2">
        {sellable !== null && (
          <Button
            disabled={selling}
            onClick={() => {
              onSell(sellable.id);
            }}
          >
            {selling ? "در حال ثبت…" : `ورود تک‌جلسه‌ای — ${formatMoney(sellable.price)}`}
          </Button>
        )}
        <Button asChild variant="outline">
          <Link to={paths.member(memberId)}>فروش اشتراک</Link>
        </Button>
      </div>
    </div>
  );
}
