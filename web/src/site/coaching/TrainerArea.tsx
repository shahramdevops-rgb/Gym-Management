import { useState } from "react";
import { Link } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { errorMessage } from "@/lib/errors";
import { formatDate } from "@/lib/format";

import { ConfirmDialog } from "../components/ConfirmDialog";
import { Person } from "../components/Person";
import { sitePaths } from "../paths";
import { useEndStudent, useStudents } from "./api";
import { matchesStudent } from "./studentSearch";

/**
 * «شاگردان من» on the trainer's home (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap
 * 15.4b; the requests are their own card since 15.7d, `TrainerRequestsCard`). The trainer sees
 * each member's name and phone; the phone opens the dialler, «حضور» the student's attendance
 * (15.7) and «برنامهٔ ثابت» their fixed weekly sessions (15.6b). A search box finds a student by
 * name or phone (15.7d). «پایان» asks first.
 */
export function TrainerArea() {
  const students = useStudents();
  const end = useEndStudent();
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

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          شاگردان من
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {end.isError && <Alert variant="destructive">{errorMessage(end.error)}</Alert>}
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
                      detail={`از ${formatDate(student.since)}`}
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
                      <ConfirmDialog
                        label="پایان"
                        question={`مربی‌گری شما برای ${student.fullName} از امروز تمام می‌شود. ادامه می‌دهید؟`}
                        pending={end.isPending}
                        onConfirm={(close) => end.mutate(student.memberId, { onSettled: close })}
                      />
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
