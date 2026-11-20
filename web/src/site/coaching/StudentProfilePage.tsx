import { ArrowRight } from "lucide-react";
import { Link, Navigate, useParams } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatPhone, gymToday } from "@/lib/format";
import { cn } from "@/lib/utils";

import { useSiteMe } from "../auth/api";
import { CallButton } from "../components/Person";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { useStudents } from "./api";
import { isActiveStudent, periodMark, periodState, periodValue, soonDaysOf } from "./period";

/**
 * A student's profile (BUSINESS_RULES.md §15 *The student's profile and every visit in the month*,
 * 15.7p), opened from their name in «شاگردان من», «شاگردان غیرفعال» and the day page: name, phone
 * (the one place its number shows, the developer, 1405/08/29; a tap on it or on the call icon calls
 * them), since when they are the trainer's, the coaching period with its last day and
 * days left, in amber when the trainer has something to do (the mark of «شاگردان من»), and the
 * buttons «حضور» and «برنامهٔ ثابت». Only what the trainer sees already: nothing of the member's plan
 * or debt. From the students' read the home and the lists make anyway (`useStudents`), so it needs no
 * request of its own; a student no longer the trainer's sends the trainer back to «شاگردان من».
 */
export function StudentProfilePage() {
  const { memberId = "" } = useParams();
  const me = useSiteMe();
  const students = useStudents();

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  const student = students.data?.students.find((row) => row.memberId === memberId);
  if (students.data !== undefined && student === undefined) {
    return <Navigate to={sitePaths.students} replace />;
  }

  const today = gymToday();
  const state = student === undefined ? null : periodState(student.periodEndsOn, today);
  const needsTrainer =
    state !== null &&
    students.data !== undefined &&
    periodMark(state, soonDaysOf(students.data)) !== null;
  // Back to the list the student is in.
  const backTo =
    student === undefined || isActiveStudent(student.periodEndsOn, today)
      ? sitePaths.students
      : sitePaths.inactiveStudents;

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={backTo} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <h1 className="flex flex-wrap items-center gap-2 text-xl font-bold">
          {student?.fullName ?? "شاگرد"}
          {student !== undefined && !student.isActive && <Badge variant="secondary">غیرفعال</Badge>}
        </h1>
      </div>

      {student === undefined || state === null ? (
        students.isError ? (
          <Alert variant="destructive">{errorMessage(students.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <Card accent={false} className="shadow-raised">
          <CardContent className="space-y-4">
            <dl className="grid grid-cols-[auto_1fr] items-baseline gap-x-4 gap-y-2.5 text-sm">
              <dt className="text-muted-foreground">تلفن</dt>
              <dd className="flex items-center gap-2">
                <a
                  href={`tel:${student.phoneNumber}`}
                  dir="ltr"
                  className="text-primary underline-offset-4 hover:underline"
                >
                  {formatPhone(student.phoneNumber)}
                </a>
                <CallButton fullName={student.fullName} phoneNumber={student.phoneNumber} />
              </dd>
              <dt className="text-muted-foreground">شاگرد از</dt>
              <dd>{formatDate(student.since)}</dd>
              <dt className="text-muted-foreground">دورهٔ مربی‌گری</dt>
              <dd className={cn(needsTrainer && "font-medium text-warning-ink")}>
                {periodValue(state)}
              </dd>
            </dl>
            <div className="flex gap-2">
              <Button asChild variant="secondary" className="flex-1">
                <Link to={sitePaths.studentAttendance(student.memberId)}>حضور</Link>
              </Button>
              <Button asChild variant="secondary" className="flex-1">
                <Link to={sitePaths.student(student.memberId)}>برنامهٔ ثابت</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </SiteFrame>
  );
}
