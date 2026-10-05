import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";

import { useMarkChequePassed } from "../api";

interface PassChequeConfirmProps {
  chequeId: string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * «پاس شد» asks once before it is sent: it is final (BUSINESS_RULES.md §9 *Cheques*), and the
 * cheque leaves the dashboard's reminder. The expense is not written for the Owner — a cheque is
 * not an expense — so the confirmation says where to record it.
 */
export function PassChequeConfirm({ chequeId, onDone, onCancel }: PassChequeConfirmProps) {
  const markPassed = useMarkChequePassed();
  const [problem, setProblem] = useState<string | null>(null);

  async function confirm() {
    setProblem(null);
    try {
      await markPassed.mutateAsync(chequeId);
      onDone();
    } catch (error) {
      setProblem(errorMessage(error));
    }
  }

  return (
    <div className="space-y-3">
      {problem !== null && <Alert variant="destructive">{problem}</Alert>}

      <Alert>
        «پاس شد» قطعی است و دیگر برنمی‌گردد. چک از یادآور داشبورد بیرون می‌رود. هزینهٔ آن را خودتان
        در{" "}
        <Link to={paths.expenses} className="font-medium underline">
          هزینه‌ها
        </Link>{" "}
        ثبت کنید.
      </Alert>

      <div className="flex gap-2">
        <Button type="button" size="sm" disabled={markPassed.isPending} onClick={confirm}>
          تأیید پاس شدن
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </div>
  );
}
