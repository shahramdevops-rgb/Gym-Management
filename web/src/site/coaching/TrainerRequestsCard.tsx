import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate, toPersianDigits } from "@/lib/format";

import { Person } from "../components/Person";
import { useAcceptTrainerRequest, useDeclineTrainerRequest, useStudents } from "./api";

/**
 * «درخواست‌ها» (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap 15.4b): the members who
 * asked for this trainer and wait for an answer, shown only when there are any. The first card of
 * the trainer's home, with the count in its title (15.7d), since the app's icon cannot show one.
 * «رد کردن» asks nothing, since the member may ask again. «شاگردان من» shows the loading and errors
 * of the list they share.
 */
export function TrainerRequestsCard() {
  const students = useStudents();
  const accept = useAcceptTrainerRequest();
  const decline = useDeclineTrainerRequest();

  const requests = students.data?.requests ?? [];
  const answering = accept.isPending || decline.isPending;
  const answerFailure = accept.error ?? decline.error;

  if (requests.length === 0 && answerFailure === null) {
    return null;
  }

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          درخواست‌ها{requests.length > 0 && ` (${toPersianDigits(requests.length)})`}
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
  );
}
