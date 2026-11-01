import { Link } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate } from "@/lib/format";

import { ConfirmDialog } from "../components/ConfirmDialog";
import { Person } from "../components/Person";
import { sitePaths } from "../paths";
import {
  useAcceptTrainerRequest,
  useDeclineTrainerRequest,
  useEndStudent,
  useStudents,
} from "./api";

/**
 * The trainer's side of the home screen (BUSINESS_RULES.md §15 *A member and their trainer*,
 * roadmap 15.4b): the requests waiting for an answer, shown only when there are any, then
 * «شاگردان من». The trainer sees each member's name and phone; the phone opens the dialler, and
 * «برنامهٔ ثابت» the student's fixed weekly sessions (15.6b).
 * «رد کردن» asks nothing, since the member may ask again; «پایان» asks first.
 */
export function TrainerArea() {
  const students = useStudents();
  const accept = useAcceptTrainerRequest();
  const decline = useDeclineTrainerRequest();
  const end = useEndStudent();

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

  const { requests, students: list } = students.data;
  const answering = accept.isPending || decline.isPending;
  const answerFailure = accept.error ?? decline.error;

  return (
    <>
      {(requests.length > 0 || answerFailure !== null) && (
        <Card accent={false} className="gap-3 shadow-raised">
          <CardHeader>
            <CardTitle role="heading" aria-level={2}>
              درخواست‌ها
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {answerFailure !== null && (
              <Alert variant="destructive">{errorMessage(answerFailure)}</Alert>
            )}
            <ul aria-label="درخواست‌ها" className="divide-y">
              {requests.map((request) => (
                <li key={request.requestId} className="space-y-2 py-3 first:pt-0 last:pb-0">
                  <Person
                    fullName={request.fullName}
                    phoneNumber={request.phoneNumber}
                    detail={`درخواست ${formatDate(request.requestedOn)}`}
                  />
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      disabled={answering}
                      onClick={() => accept.mutate(request.requestId)}
                    >
                      پذیرفتن
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={answering}
                      onClick={() => decline.mutate(request.requestId)}
                    >
                      رد کردن
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

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
            <ul aria-label="شاگردان من" className="divide-y">
              {list.map((student) => (
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
        </CardContent>
      </Card>
    </>
  );
}
