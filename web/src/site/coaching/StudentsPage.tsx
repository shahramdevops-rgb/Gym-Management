import { ArrowRight, ChevronDown } from "lucide-react";
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
import { cn } from "@/lib/utils";

import { useSiteMe } from "../auth/api";
import { Person } from "../components/Person";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { useStudents, type Students } from "./api";
import { isActiveStudent, periodMark, periodState, soonDaysOf } from "./period";
import { matchesStudent } from "./studentSearch";

type Student = Students["students"][number];

/**
 * The trainer's «شاگردان من», a page of its own since 1405/08/20 (the developer: the home showed the
 * whole list; now a card on it with the count opens this page, lazy, so its code comes only when
 * opened). Each member's name and phone; the phone opens the dialler, «حضور» the student's
 * attendance (15.7) and «برنامه» their «برنامهٔ ثابت», the fixed weekly sessions (15.6b; the
 * button's short name, the developer's, 1405/08/20). A search box finds a
 * student by name or phone (15.7d). «پایان مربی‌گری» is at the foot of «برنامهٔ ثابت» (15.7g). A
 * student whose coaching period needs the trainer is marked: not given yet, ending within the SMS
 * settings' days for the period's last days (15.7n, three days before it), or over (15.7h).
 *
 * In two parts since 15.7l (BUSINESS_RULES.md §15 *«شاگردان من» in two parts*, so the list does not
 * fill up): the active students, and under them a closed part «غیرفعال (n)», those whose period is
 * over and was not renewed, which opens on a tap (no tab). The title counts the active ones, as the
 * home's card does. The search looks in both, and opens the closed part while it finds someone there.
 */
export function StudentsPage() {
  const me = useSiteMe();
  const students = useStudents();
  const [query, setQuery] = useState("");

  if (me.data !== undefined && me.data.trainer === null) {
    return <Navigate to={sitePaths.home} replace />;
  }

  const list = students.data?.students;
  const soonDays = students.data === undefined ? 0 : soonDaysOf(students.data);
  const today = gymToday();
  const active = list?.filter((student) => isActiveStudent(student.periodEndsOn, today));

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.home} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <h1 className="text-xl font-bold">
          شاگردان من{active === undefined ? "" : ` (${toPersianDigits(active.length)})`}
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
              <StudentParts
                students={list}
                today={today}
                soonDays={soonDays}
                query={query}
                onQuery={setQuery}
              />
            )}
          </CardContent>
        </Card>
      )}
    </SiteFrame>
  );
}

function StudentParts({
  students,
  today,
  soonDays,
  query,
  onQuery,
}: {
  students: Students["students"];
  today: string;
  soonDays: number;
  query: string;
  onQuery: (query: string) => void;
}) {
  const [inactiveOpen, setInactiveOpen] = useState(false);

  const searching = query.trim() !== "";
  const found = students.filter((student) => matchesStudent(student, query));
  const active = found.filter((student) => isActiveStudent(student.periodEndsOn, today));
  const inactive = found.filter((student) => !isActiveStudent(student.periodEndsOn, today));
  // While searching, a match among the inactive ones is shown without a tap: hidden in a closed
  // part, the search would seem to have found nothing.
  const showInactive = inactiveOpen || searching;

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
      ) : active.length > 0 ? (
        <StudentRows label="شاگردان من" students={active} today={today} soonDays={soonDays} />
      ) : (
        !searching && <p className="text-sm text-muted-foreground">شاگرد فعالی ندارید.</p>
      )}

      {inactive.length > 0 && (
        <div className="border-t pt-3">
          <button
            type="button"
            aria-expanded={showInactive}
            aria-controls="inactive-students"
            onClick={() => setInactiveOpen((open) => !open)}
            className="flex w-full items-center justify-between gap-2 rounded-md py-1 text-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/30 focus-visible:outline-none"
          >
            غیرفعال ({toPersianDigits(inactive.length)})
            <ChevronDown
              aria-hidden
              className={cn("size-4 transition-transform", showInactive && "rotate-180")}
            />
          </button>
          {showInactive && (
            <div id="inactive-students" className="pt-3">
              <StudentRows
                label="شاگردان غیرفعال"
                students={inactive}
                today={today}
                soonDays={soonDays}
              />
            </div>
          )}
        </div>
      )}
    </>
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
