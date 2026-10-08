import { Alert } from "@/components/ui/alert";
import { formatMoney } from "@/lib/format";

import { useSmsCredit } from "../api";

/**
 * The Kavenegar account's remaining credit, on both SMS pages (BUSINESS_RULES.md §10 *Sending*). In
 * test mode (`Sms:Provider` is `Fake`) nothing is really sent, and the line says so instead.
 *
 * The warning comes from the messages, not from Kavenegar: the latest one that failed for credit
 * used up is newer than the latest one sent. So it shows even when Kavenegar does not answer, and
 * goes away by itself once a message is sent again.
 */
export function SmsCreditLine() {
  const credit = useSmsCredit();

  if (credit.isPending) {
    return null;
  }

  if (credit.isError) {
    return <p className="text-sm text-muted-foreground">اعتبار پنل پیامک الان در دسترس نیست.</p>;
  }

  return (
    <div className="space-y-2">
      {credit.data.creditUsedUp && (
        <Alert variant="destructive" role="alert">
          اعتبار پنل پیامک تمام شده و آخرین پیامک‌ها فرستاده نشدند. پنل کاوه‌نگار را شارژ کنید؛ بعد
          از آن پیامک‌های ناموفق را از «پیامک‌ها» دوباره بفرستید.
        </Alert>
      )}
      {credit.data.isTestMode ? (
        <Alert role="status">حالت آزمایشی: پیامکی واقعاً فرستاده نمی‌شود و فقط ثبت می‌شود.</Alert>
      ) : credit.data.remainingToman === null ? (
        <p className="text-sm text-muted-foreground">اعتبار پنل پیامک الان در دسترس نیست.</p>
      ) : (
        <p className="text-sm">
          اعتبار باقی‌ماندهٔ پنل پیامک:{" "}
          <span className="font-medium">{formatMoney(credit.data.remainingToman)}</span>
        </p>
      )}
    </div>
  );
}
