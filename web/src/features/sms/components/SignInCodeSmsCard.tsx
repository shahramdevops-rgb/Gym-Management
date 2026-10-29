import { CalendarDays, Coins, Smartphone } from "lucide-react";

import { StatTile } from "@/components/StatTile";
import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatMoney, formatNumber } from "@/lib/format";

import { useSignInCodeSms } from "../api";

/**
 * The site's sign-in code SMS, «کد ورود سایت» (BUSINESS_RULES.md §15, «The code SMS in the
 * panel»). They are not in the history below: a code is never written anywhere readable, so only
 * their count and cost are shown. Reaching the gym's 300 a day means an attack, not a busy day, so
 * it is said in red.
 */
export function SignInCodeSmsCard() {
  const codes = useSignInCodeSms();

  if (codes.isPending) {
    return null;
  }

  if (codes.isError) {
    return (
      <p className="text-sm text-muted-foreground">
        آمار پیامک‌های کد ورود سایت الان در دسترس نیست.
      </p>
    );
  }

  const { today, dailyLimit, thisMonth, thisMonthCostToman, limitReached } = codes.data;

  return (
    <Card>
      <CardHeader>
        <CardTitle>کد ورود سایت</CardTitle>
        <CardDescription>
          پیامک‌های کد یک‌بارمصرف ورود به سایت. خود کدها جایی نوشته نمی‌شوند و در فهرست زیر نیستند.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {limitReached === true && (
          <Alert variant="destructive" role="alert">
            سقف {formatNumber(Number(dailyLimit))} پیامک کد ورود امروز پر شده است؛ تا فردا برای کسی
            کد ورود فرستاده نمی‌شود.
          </Alert>
        )}
        <dl className="grid gap-3 sm:grid-cols-3">
          <StatTile
            icon={Smartphone}
            label="امروز"
            value={`${formatNumber(Number(today))} از ${formatNumber(Number(dailyLimit))}`}
            valueLabel="پیامک‌های کد ورود امروز"
          />
          <StatTile
            icon={CalendarDays}
            label="این ماه"
            value={`${formatNumber(Number(thisMonth))} پیامک`}
            valueLabel="پیامک‌های کد ورود این ماه"
          />
          <StatTile
            icon={Coins}
            label="هزینهٔ این ماه"
            value={formatMoney(thisMonthCostToman)}
            valueLabel="هزینهٔ پیامک‌های کد ورود این ماه"
          />
        </dl>
      </CardContent>
    </Card>
  );
}
