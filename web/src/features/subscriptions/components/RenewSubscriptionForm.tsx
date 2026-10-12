import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { usePrices } from "@/features/settings/api";
import { toneButton } from "@/lib/edges";
import { errorMessage } from "@/lib/errors";
import { formatMoney } from "@/lib/format";
import { multiplyMoney } from "@/lib/money";

import { useRenewSubscription, type Subscription } from "../api";
import { planLabel } from "../planLabel";

interface RenewSubscriptionFormProps {
  memberId: string;
  /** The subscription the card shows: what the member will get again. */
  current: Subscription;
  onDone: (renewed: Subscription) => void;
  onCancel: () => void;
}

/**
 * Opens under the current subscription card: confirm before renewing, so a stray click on
 * "تمدید" cannot queue several subscriptions in a row (each renew call queues one more,
 * BUSINESS_RULES.md §4). Renewing sells the same days and sessions again at today's session price.
 * The exact start date depends on the member's existing subscriptions and is computed by the
 * server, not guessed here — `onDone` receives the real result to show it.
 */
export function RenewSubscriptionForm({
  memberId,
  current,
  onDone,
  onCancel,
}: RenewSubscriptionFormProps) {
  const renew = useRenewSubscription();
  const prices = usePrices();
  const [error, setError] = useState<string | null>(null);

  async function confirm() {
    setError(null);
    try {
      const renewed = await renew.mutateAsync({ memberId });
      onDone(renewed);
    } catch (problem) {
      setError(errorMessage(problem));
    }
  }

  const sessionPrice = prices.data?.sessionPrice ?? null;

  return (
    <div className="space-y-3 rounded-md border p-3">
      {error !== null && <Alert variant="destructive">{error}</Alert>}
      {/* Three short lines rather than one paragraph (the UX review, 1405/07/21): what is sold,
          what it costs, when it starts. The price is the line the desk reads out. */}
      <dl className="space-y-1 text-sm">
        <div className="flex flex-wrap gap-x-2">
          <dt className="text-muted-foreground">پلن:</dt>
          <dd className="font-medium">
            {/* Renew skips single visits and sells the member's last plan (BUSINESS_RULES.md §4). */}
            {current.isSingleSession
              ? "آخرین پلن این عضو (بدون ورودهای تک‌جلسه‌ای)"
              : `همان پلن «${planLabel(current)}»`}
          </dd>
        </div>
        <div className="flex flex-wrap gap-x-2">
          <dt className="text-muted-foreground">مبلغ:</dt>
          <dd className="font-bold">
            {!current.isSingleSession && sessionPrice !== null
              ? formatMoney(multiplyMoney(sessionPrice, Number(current.totalSessions)))
              : "با قیمت امروزِ هر جلسه"}
          </dd>
        </div>
        <div className="flex flex-wrap gap-x-2">
          <dt className="text-muted-foreground">شروع:</dt>
          <dd>
            بعد از اشتراک فعلی، اگر هنوز جا دارد؛ وگرنه از امروز. تاریخ دقیق بعد از تأیید همین‌جا
            می‌آید.
          </dd>
        </div>
      </dl>
      <div className="flex gap-2">
        <Button
          size="sm"
          variant="ghost"
          className={toneButton}
          disabled={renew.isPending}
          onClick={() => void confirm()}
        >
          تأیید تمدید
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </div>
  );
}
