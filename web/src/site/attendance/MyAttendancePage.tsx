import { ArrowRight } from "lucide-react";
import { useState } from "react";
import { Link, Navigate } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import { gymToday } from "@/lib/format";

import { useSiteMe } from "../auth/api";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { useMyAttendance } from "./api";
import { AttendanceMonth, MonthNavigator } from "./AttendanceMonth";
import { monthOf, monthRange } from "./labels";

/**
 * «حضور من» (BUSINESS_RULES.md §15 *The member's own attendance*, roadmap 15.7j): the member's own
 * sessions of one Jalali month up to today, each with its state as their trainer sees it, and the
 * month's totals. Read only, from «سانس‌های من». The visits outside the sessions are the trainer's
 * alone. Only a member reaches it.
 */
export function MyAttendancePage() {
  const me = useSiteMe();
  const thisMonth = monthOf(gymToday());
  const [month, setMonth] = useState(thisMonth);
  const { from, to } = monthRange(month);
  const attendance = useMyAttendance(from, to);

  if (me.data !== undefined && me.data.member === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.mySessions} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <h1 className="text-xl font-bold">حضور من</h1>
      </div>

      <MonthNavigator month={month} thisMonth={thisMonth} onChange={setMonth} />

      {attendance.data === undefined ? (
        attendance.isError ? (
          <Alert variant="destructive">{errorMessage(attendance.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <AttendanceMonth
          totals={attendance.data.totals}
          sessions={attendance.data.sessions}
          empty="در این ماه سانسی نداشته‌اید."
        />
      )}
    </SiteFrame>
  );
}
