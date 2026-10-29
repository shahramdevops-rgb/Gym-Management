import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatLongDate, formatPhone } from "@/lib/format";

import { useMyDetails } from "./api";

/**
 * «مشخصات من» (BUSINESS_RULES.md §15 *The member's own records*): read-only, the birth date least
 * of all, since it decides the birthday SMS and the free visit (§10). The desk corrects them.
 */
export function MyDetailsCard() {
  const details = useMyDetails();

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          مشخصات من
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {details.data === undefined ? (
          details.isError ? (
            <Alert variant="destructive">{errorMessage(details.error)}</Alert>
          ) : (
            <LoadingState className="py-2" />
          )
        ) : (
          <>
            <dl className="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-2 text-sm">
              <dt className="text-muted-foreground">نام</dt>
              <dd>{details.data.fullName}</dd>

              <dt className="text-muted-foreground">موبایل</dt>
              <dd>
                <span dir="ltr">{formatPhone(details.data.phoneNumber)}</span>
              </dd>

              <dt className="text-muted-foreground">تاریخ تولد</dt>
              <dd>{formatDate(details.data.birthDate)}</dd>

              <dt className="text-muted-foreground">عضو باشگاه از</dt>
              <dd>{formatLongDate(new Date(details.data.joinedAt))}</dd>

              <dt className="text-muted-foreground">ثبت‌نام در سایت</dt>
              <dd>{formatLongDate(new Date(details.data.signedUpAt))}</dd>
            </dl>
            <p className="text-sm text-muted-foreground">
              برای اصلاح مشخصات به پذیرش باشگاه مراجعه کنید.
            </p>
          </>
        )}
      </CardContent>
    </Card>
  );
}
