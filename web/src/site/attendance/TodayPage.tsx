import { ArrowRight } from "lucide-react";
import { Link, Navigate } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import { formatTime } from "@/lib/format";

import { useSiteMe } from "../auth/api";
import { Person } from "../components/Person";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { formatSessionTimes } from "../timetable/labels";
import { VipBadge } from "../timetable/VipBadge";
import { useTrainerToday, type TodaySession } from "./api";
import { PresenceBadge } from "./PresenceBadge";

/**
 * «حضور امروز» (BUSINESS_RULES.md §15 *Attendance for the trainer*, roadmap 15.7b): today's
 * sessions, each in its own box with who is in it and whether they have checked in at the desk.
 * It asks again every minute while it is open, and says when it last did. Only a trainer reaches it.
 */
export function TodayPage() {
  const me = useSiteMe();
  const today = useTrainerToday({ refresh: true });

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.home} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <h1 className="text-xl font-bold">حضور امروز</h1>
      </div>

      {today.data === undefined ? (
        today.isError ? (
          <Alert variant="destructive">{errorMessage(today.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <>
          {today.isError && (
            <Alert variant="destructive">
              به‌روز نشد؛ آنچه می‌بینید از ساعت{" "}
              {formatTime(new Date(today.dataUpdatedAt).toISOString())} است.
            </Alert>
          )}
          {today.data.sessions.length === 0 ? (
            <p className="text-sm text-muted-foreground">امروز سانسی ندارید.</p>
          ) : (
            <ul aria-label="سانس‌های امروز" className="space-y-3">
              {today.data.sessions.map((session) => (
                <SessionBox
                  key={`${session.start}-${session.end}-${session.removed}`}
                  session={session}
                />
              ))}
            </ul>
          )}
          <p className="text-center text-xs text-muted-foreground">
            هر دقیقه به‌روز می‌شود · آخرین بار ساعت{" "}
            {formatTime(new Date(today.dataUpdatedAt).toISOString())}
          </p>
        </>
      )}
    </SiteFrame>
  );
}

function SessionBox({ session }: { session: TodaySession }) {
  const times = formatSessionTimes(session);

  return (
    <li aria-label={times} className="space-y-2 rounded-xl border bg-card p-3 shadow-raised">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-semibold">{times}</span>
        {session.isVip && <VipBadge />}
        {session.removed ? (
          <Badge variant="destructive">لغو شد</Badge>
        ) : session.ended ? (
          <Badge variant="outline">تمام شد</Badge>
        ) : (
          session.started && <Badge variant="outline">در حال برگزاری</Badge>
        )}
      </div>
      {session.students.length === 0 ? (
        <p className="text-sm text-muted-foreground">شاگردی در این سانس نیست.</p>
      ) : (
        <ul aria-label={`شاگردان ${times}`} className="divide-y">
          {session.students.map((student) => (
            <li
              key={student.memberId}
              className="flex items-center justify-between gap-2 py-2 first:pt-0 last:pb-0"
            >
              <Person fullName={student.fullName} phoneNumber={student.phoneNumber} />
              <PresenceBadge
                state={student.state}
                checkedInAt={student.checkedInAt}
                cameAfter={student.cameAfter}
              />
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}
