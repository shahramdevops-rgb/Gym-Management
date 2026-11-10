import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";

import { Person } from "../components/Person";
import { formatDayLabel, formatSessionTimes } from "../timetable/labels";
import { useAnswerLeaveRequest, useLeaveRequests } from "./api";

/**
 * «درخواست‌های مرخصی» on the trainer's home (BUSINESS_RULES.md §15 *Leave by request*, roadmap
 * 15.7i): the students asking leave from a session, the nearest first, each with «قبول» and «رد».
 * Shown only when there are any; the count is in the title, as for the coaching requests. Neither
 * button asks first: a decline leaves the student in the session as they were, and an accepted leave
 * the student can take back with «می‌آیم». A request not answered by the session's start is declined
 * and leaves the card by itself.
 */
export function LeaveRequestsCard() {
  const requests = useLeaveRequests();
  const answer = useAnswerLeaveRequest();

  const items = requests.data?.requests ?? [];
  if (items.length === 0 && !answer.isError) {
    return null;
  }

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          درخواست‌های مرخصی{items.length > 0 && ` (${toPersianDigits(items.length)})`}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {answer.isError && <Alert variant="destructive">{errorMessage(answer.error)}</Alert>}
        <ul aria-label="درخواست‌های مرخصی" className="divide-y">
          {items.map((request) => (
            <li key={request.requestId} className="space-y-2 py-3 first:pt-0 last:pb-0">
              <Person
                fullName={request.fullName}
                phoneNumber={request.phoneNumber}
                detail={`${formatDayLabel(request.date)}، ${formatSessionTimes(request)}`}
              />
              <div className="flex gap-2">
                <Button
                  size="sm"
                  disabled={answer.isPending}
                  onClick={() => answer.mutate({ requestId: request.requestId, accept: true })}
                >
                  قبول
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={answer.isPending}
                  onClick={() => answer.mutate({ requestId: request.requestId, accept: false })}
                >
                  رد
                </Button>
              </div>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
