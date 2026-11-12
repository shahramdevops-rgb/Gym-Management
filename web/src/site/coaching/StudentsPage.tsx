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
import { periodMark, periodState } from "./period";
import { matchesStudent } from "./studentSearch";

/**
 * The trainer's «شاگردان من», a page of its own since 1405/08/20 (the developer: the home showed the
 * whole list; now a card on it with the count opens this page, lazy, so its code comes only when
 * opened). Each member's name and phone; the phone opens the dialler, «حضور» the student's
 * attendance (15.7) and «برنامه» their «برنامهٔ ثابت», the fixed weekly sessions (15.6b; the
 * button's short name, the developer's, 1405/08/20). A search box finds a
 * student by name or phone (15.7d). «پایان مربی‌گری» is at the foot of «برنامهٔ ثابت» (15.7g). A
 * student whose coaching period needs the trainer is marked: not given yet, ending within three
 * days, or over (15.7h).
 */
export function StudentsPage() {
  const me = useSiteMe();
  const students = useStudents();
  const [query, setQuery] = useState("");

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  const list = students.data?.students;

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.home} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <h1 className="text-xl font-bold">
          شاگردان من{list === undefined ? "" : ` (${toPersianDigits(list.length)})`}
        </h1>
      </div>

      {list === undefined ? (
        students.isError ? (
          <Alert variant="destructive">{errorMessage(students.error)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : (
        <Card accent={false} className="shadow-raised">
          <CardContent className="space-y-3">
            {list.length === 0 ? (
              <p className="text-sm text-muted-foreground">هنوز شاگردی ندارید.</p>
            ) : (
              <StudentList students={list} query={query} onQuery={setQuery} />
            )}
          </CardContent>
        </Card>
      )}
    </SiteFrame>
  );
}

function StudentList({
  students,
  query,
  onQuery,
}: {
  students: Students["students"];
  query: string;
  onQuery: (query: string) => void;
}) {
  const found = students.filter((student) => matchesStudent(student, query));
  const today = gymToday();

  return (
    <>
      <Input
        type="search"
        aria-label="جست‌وجوی شاگرد"
        placeholder="نام یا شماره"
        value={query}
        onChange={(event) => onQuery(event.target.value)}
      />
      {found.length === 0 ? (
        <p className="text-sm text-muted-foreground">شاگردی با این نام یا شماره پیدا نشد.</p>
      ) : (
        <ul aria-label="شاگردان من" className="divide-y">
          {found.map((student) => (
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
                  detail={<StudentDetail student={student} today={today} />}
                  inactive={!student.isActive}
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
      )}
    </>
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
