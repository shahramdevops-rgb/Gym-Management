import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";

import { useRenewSubscription, type Subscription } from "../api";

interface RenewSubscriptionFormProps {
  memberId: string;
  planName: string;
  onDone: (renewed: Subscription) => void;
  onCancel: () => void;
}

/**
 * Opens under the current subscription card: confirm before renewing, so a stray click on
 * "تمدید" cannot queue several subscriptions in a row (each renew call queues one more,
 * BUSINESS_RULES.md §4). The exact start date depends on the member's existing subscriptions and
 * is computed by the server, not guessed here — `onDone` receives the real result to show it.
 */
export function RenewSubscriptionForm({ memberId, planName, onDone, onCancel }: RenewSubscriptionFormProps) {
  const renew = useRenewSubscription();
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

  return (
    <div className="space-y-3 rounded-md border p-3">
      {error !== null && <Alert variant="destructive">{error}</Alert>}
      <p className="text-sm text-muted-foreground">
        همان پلن «{planName}» با قیمت و شرایط فعلی آن تمدید می‌شود. تاریخ شروع اشتراک تازه
        خودکار محاسبه می‌شود — اگر اشتراک فعلی این عضو هنوز جا دارد، پشت سر آن قرار می‌گیرد؛
        وگرنه از همین امروز شروع می‌شود. بعد از تأیید، تاریخ دقیق همین‌جا نشان داده می‌شود.
      </p>
      <div className="flex gap-2">
        <Button size="sm" disabled={renew.isPending} onClick={() => void confirm()}>
          تأیید تمدید
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </div>
  );
}
