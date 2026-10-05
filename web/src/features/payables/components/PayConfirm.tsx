import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";

import { paidLabel, useMarkPayablePaid, type PayableKind } from "../api";

interface PayConfirmProps {
  payableId: string;
  kind: PayableKind;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * «پاس شد» / «پرداخت شد» asks once before it is sent: it writes an expense dated today, and the
 * row leaves the dashboard's reminder (BUSINESS_RULES.md §9 *Cheques and instalments*).
 */
export function PayConfirm({ payableId, kind, onDone, onCancel }: PayConfirmProps) {
  const markPaid = useMarkPayablePaid();
  const [problem, setProblem] = useState<string | null>(null);

  async function confirm() {
    setProblem(null);
    try {
      await markPaid.mutateAsync(payableId);
      onDone();
    } catch (error) {
      setProblem(errorMessage(error));
    }
  }

  return (
    <div className="space-y-3">
      {problem !== null && <Alert variant="destructive">{problem}</Alert>}

      <Alert>
        با تأیید، مبلغ با تاریخ امروز در هزینه‌ها ثبت می‌شود و از یادآور داشبورد بیرون می‌رود. اگر
        اشتباه زده شد، با «برگشت به در انتظار» برمی‌گردد.
      </Alert>

      <div className="flex gap-2">
        <Button type="button" size="sm" disabled={markPaid.isPending} onClick={confirm}>
          تأیید {paidLabel(kind)}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </div>
  );
}
