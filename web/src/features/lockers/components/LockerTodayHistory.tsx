import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { guestLabel } from "@/features/attendance/holder";
import { errorMessage } from "@/lib/errors";
import { formatTime } from "@/lib/format";

import { useLockerVisitsToday, type LockerVisit } from "../api";

/**
 * Everyone who had this locker today, oldest first, each name opening that member's profile (a
 * guest is marked «مهمان», with no profile to open)
 * (BUSINESS_RULES.md §6 *Who had a locker today*): the question the desk asks when something is
 * left behind or found broken. Times only, no date: the list is about today and nothing else.
 */
export function LockerTodayHistory({ lockerId }: { lockerId: string }) {
  const visits = useLockerVisitsToday(lockerId);

  if (visits.isPending) {
    return <p className="text-sm text-muted-foreground">در حال بارگذاری…</p>;
  }

  if (visits.isError) {
    return <Alert variant="destructive">{errorMessage(visits.error)}</Alert>;
  }

  if (visits.data.length === 0) {
    return <p className="text-sm text-muted-foreground">امروز کسی از این کمد استفاده نکرده است.</p>;
  }

  return (
    <ul className="divide-y rounded-md border" aria-label="تاریخچه امروز کمد">
      {visits.data.map((visit) => (
        <li key={visit.attendanceId} className="flex items-center justify-between gap-3 px-3 py-2">
          <span className="flex flex-wrap items-center gap-2">
            {visit.memberId === null ? (
              // A guest has no profile to open (BUSINESS_RULES.md §7 *Guest visit*).
              <>
                <span className="font-medium">{visit.guestName}</span>
                <Badge variant="outline">{guestLabel}</Badge>
              </>
            ) : (
              <Link
                to={paths.member(visit.memberId)}
                className="font-medium underline-offset-4 hover:underline"
              >
                {visit.memberFullName}
              </Link>
            )}
            {visit.cancelledAt !== null && visit.cancelledAt !== undefined && (
              <Badge variant="secondary">لغو شده</Badge>
            )}
          </span>
          <span className="text-sm text-muted-foreground">{visitTimes(visit)}</span>
        </li>
      ))}
    </ul>
  );
}

/** "ورود ۰۹:۱۵ · خروج ۱۰:۴۰", or that the member is still inside. */
function visitTimes(visit: LockerVisit): string {
  const checkedIn = `ورود ${formatTime(visit.checkedInAt)}`;

  return visit.checkedOutAt === null || visit.checkedOutAt === undefined
    ? `${checkedIn} · هنوز داخل`
    : `${checkedIn} · خروج ${formatTime(visit.checkedOutAt)}`;
}
