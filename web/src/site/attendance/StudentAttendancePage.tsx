import { ArrowRight } from "lucide-react";
import { useState } from "react";
import { Link, Navigate, useParams } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import { gymToday } from "@/lib/format";

import { useSiteMe } from "../auth/api";
import { useStudents } from "../coaching/api";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { useStudentAttendance } from "./api";
import { AttendanceMonth, MonthNavigator } from "./AttendanceMonth";
import { monthOf, monthRange } from "./labels";

/**
 * A student's «حضور» (BUSINESS_RULES.md §15 *Attendance for the trainer*, roadmap 15.7b): their
 * sessions of one Jalali month up to today, each with its state, and the month's totals in small
 * boxes. A session with an earlier trainer shows that trainer's name. The month moves back as far
 * as the trainer likes and forward up to this one. Under them, «خارج از سانس» (15.7d): the visits
 * that counted for none of the student's sessions. Only a trainer reaches it.
 */
export function StudentAttendancePage() {
  const { memberId = "" } = useParams();
  const me = useSiteMe();
  const students = useStudents();
  const thisMonth = monthOf(gymToday());
  const [month, setMonth] = useState(thisMonth);
  const { from, to } = monthRange(month);
  const attendance = useStudentAttendance(memberId, from, to);

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  const fullName = students.data?.students.find(
    (student) => student.memberId === memberId,
  )?.fullName;

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.students} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <div>
          <h1 className="text-xl font-bold">حضور</h1>
          {fullName !== undefined && <p className="text-sm text-muted-foreground">{fullName}</p>}
        </div>
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
          outside={attendance.data.outside}
          empty="در این ماه سانسی نداشته است."
        />
      )}
    </SiteFrame>
  );
}
