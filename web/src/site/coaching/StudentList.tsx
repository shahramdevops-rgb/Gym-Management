import { ArrowRight } from "lucide-react";
import { useState } from "react";
import { Link, Navigate } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { errorMessage } from "@/lib/errors";
import { formatDate, gymToday, toPersianDigits } from "@/lib/format";

import { useSiteMe } from "../auth/api";
import { Person } from "../components/Person";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { useStudents, type Students } from "./api";
import { periodMark, periodState, soonDaysOf, studentsOf, type StudentPart } from "./period";
import { matchesStudent } from "./studentSearch";

type Student = Students["students"][number];

const titles: Record<StudentPart, string> = {
  active: "شاگردان من",
  inactive: "شاگردان غیرفعال",
};

/**
 * A page of the trainer's students: «شاگردان من», the active ones, or «شاگردان غیرفعال», those
 * whose coaching period is over and was not renewed (BUSINESS_RULES.md §15 *The student's profile
 * and every visit in the month*, 15.7p, in place of 15.7l's closed part). The title counts them, as
 * the home's cards do. Each member's name opens their profile (15.7p) and the phone the dialler;
 * «حضور» the student's attendance (15.7) and «برنامه» their «برنامهٔ ثابت», the fixed weekly
 * sessions (15.6b; the button's short name, the developer's, 1405/08/20). A search box finds a
 * student of this page by name or phone (15.7d). «پایان مربی‌گری» is at the foot of «برنامهٔ ثابت»
 * (15.7g). A student whose coaching period needs the trainer is marked: not given yet, ending within
 * the SMS settings' days for the period's last days (15.7n), or over (15.7h).
 */
export function StudentListPage({ part }: { part: StudentPart }) {
  const me = useSiteMe();
  const students = useStudents();
  const [query, setQuery] = useState("");

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  const list = students.data?.students;
  const soonDays = students.data === undefined ? 0 : soonDaysOf(students.data);
  const today = gymToday();
  const shown = list === undefined ? undefined : studentsOf(list, part, today);
  const found = shown?.filter((student) => matchesStudent(student, query)) ?? [];

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.home} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <h1 className="text-xl font-bold">
          {titles[part]}
          {shown === undefined ? "" : ` (${toPersianDigits(shown.length)})`}
        </h1>
      </div>

      {list === undefined || shown === undefined ? (
        students.isError ? (
          <Alert variant="destructive">{errorMessage(students.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <Card accent={false} className="shadow-raised">
          <CardContent className="space-y-3">
            {shown.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {part === "inactive"
                  ? "شاگرد غیرفعالی ندارید."
                  : list.length === 0
                    ? "هنوز شاگردی ندارید."
                    : "شاگرد فعالی ندارید."}
              </p>
            ) : (
              <>
                <Input
                  type="search"
                  aria-label="جست‌وجوی شاگرد"
                  placeholder="نام یا شماره"
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                />
                {found.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    شاگردی با این نام یا شماره پیدا نشد.
                  </p>
                ) : (
                  <StudentRows
                    label={titles[part]}
                    students={found}
                    today={today}
                    soonDays={soonDays}
                  />
                )}
              </>
            )}
          </CardContent>
        </Card>
      )}
    </SiteFrame>
  );
}

function StudentRows({
  label,
  students,
  today,
  soonDays,
}: {
  label: string;
  students: Student[];
  today: string;
  soonDays: number;
}) {
  return (
    <ul aria-label={label} className="divide-y">
      {students.map((student) => (
        // The buttons stay on the student's line whatever its length (the developer, 1405/08/20):
        // the name and its marks wrap in their own column instead.
        <li
          key={student.memberId}
          className="flex items-center justify-between gap-2 py-3 first:pt-0 last:pb-0"
        >
          <div className="min-w-0 flex-1">
            <Person
              fullName={student.fullName}
              phoneNumber={student.phoneNumber}
              detail={<StudentDetail student={student} today={today} soonDays={soonDays} />}
              inactive={!student.isActive}
              to={sitePaths.studentProfile(student.memberId)}
            />
          </div>
          <div className="flex shrink-0 gap-2">
            <Button asChild variant="secondary" size="sm">
              <Link
                to={sitePaths.studentAttendance(student.memberId)}
                aria-label={`حضور ${student.fullName}`}
              >
                حضور
              </Link>
            </Button>
            <Button asChild variant="secondary" size="sm">
              <Link
                to={sitePaths.student(student.memberId)}
                aria-label={`برنامهٔ ثابت ${student.fullName}`}
              >
                برنامه
              </Link>
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
}

/** «از ۱ آبان», and the coaching period's mark when the trainer has something to do about it. */
function StudentDetail({
  student,
  today,
  soonDays,
}: {
  student: Student;
  today: string;
  soonDays: number;
}) {
  const mark = periodMark(periodState(student.periodEndsOn, today), soonDays);

  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      از {formatDate(student.since)}
      {mark !== null && <Badge variant="warning">{mark}</Badge>}
    </span>
  );
}
