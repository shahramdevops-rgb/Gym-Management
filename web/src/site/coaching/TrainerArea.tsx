import { useState } from "react";
import { Link } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { errorMessage } from "@/lib/errors";
import { formatDate, gymToday } from "@/lib/format";

import { Person } from "../components/Person";
import { sitePaths } from "../paths";
import { useStudents, type Students } from "./api";
import { periodMark, periodState } from "./period";
import { matchesStudent } from "./studentSearch";

/**
 * «شاگردان من» on the trainer's home (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap
 * 15.4b; the requests are their own card since 15.7d, `TrainerRequestsCard`). The trainer sees
 * each member's name and phone; the phone opens the dialler, «حضور» the student's attendance
 * (15.7) and «برنامهٔ ثابت» their fixed weekly sessions (15.6b). A search box finds a student by
 * name or phone (15.7d). «پایان مربی‌گری» is no longer here but at the foot of «برنامهٔ ثابت», so a
 * finger cannot hit it by mistake (15.7g). A student whose coaching period needs the trainer is
 * marked: not given yet, ending within three days, or over (15.7h).
 */
export function TrainerArea() {
  const students = useStudents();
  const [query, setQuery] = useState("");

  if (students.data === undefined) {
    return (
      <Card accent={false} className="gap-3 shadow-raised">
        <CardHeader>
          <CardTitle role="heading" aria-level={2}>
            شاگردان من
          </CardTitle>
        </CardHeader>
        <CardContent>
          {students.isError ? (
            <Alert variant="destructive">{errorMessage(students.error)}</Alert>
          ) : (
            <LoadingState className="py-2" />
          )}
        </CardContent>
      </Card>
    );
  }

  const list = students.data.students;
  const found = list.filter((student) => matchesStudent(student, query));
  const today = gymToday();

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          شاگردان من
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {list.length === 0 ? (
          <p className="text-sm text-muted-foreground">هنوز شاگردی ندارید.</p>
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
              <p className="text-sm text-muted-foreground">شاگردی با این نام یا شماره پیدا نشد.</p>
            ) : (
              <ul aria-label="شاگردان من" className="divide-y">
                {found.map((student) => (
                  <li
                    key={student.memberId}
                    className="flex flex-wrap items-center justify-between gap-2 py-3 first:pt-0 last:pb-0"
                  >
                    <Person
                      fullName={student.fullName}
                      phoneNumber={student.phoneNumber}
                      detail={<StudentDetail student={student} today={today} />}
                      inactive={!student.isActive}
                    />
                    <div className="flex gap-2">
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
                          برنامهٔ ثابت
                        </Link>
                      </Button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

/** «از ۱ آبان», and the coaching period's mark when the trainer has something to do about it. */
function StudentDetail({
  student,
  today,
}: {
  student: Students["students"][number];
  today: string;
}) {
  const mark = periodMark(periodState(student.periodEndsOn, today));

  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      از {formatDate(student.since)}
      {mark !== null && <Badge variant="warning">{mark}</Badge>}
    </span>
  );
}
