import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate, gymToday, toPersianDigits } from "@/lib/format";

import { Person } from "../components/Person";
import {
  useAcceptRenewalRequest,
  useAcceptTrainerRequest,
  useDeclineRenewalRequest,
  useDeclineTrainerRequest,
  useStudents,
  type RenewalRequestItem,
} from "./api";
import {
  nextPeriodStart,
  periodDefaultDays,
  periodEnd,
  periodMinDays,
  periodState,
  renewQuestion,
} from "./period";
import { PeriodDaysForm } from "./PeriodDaysForm";
import { PeriodQuestion } from "./PeriodQuestion";

/**
 * «درخواست‌ها» (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap 15.4b): the members who
 * asked for this trainer and wait for an answer, shown only when there are any. The first card of
 * the trainer's home, with the count in its title (15.7d), since the app's icon cannot show one.
 * «رد کردن» asks nothing, since the member may ask again. «پذیرفتن» opens the box of the coaching
 * period's days, 30 to start with (15.7h). «شاگردان من» shows the loading and errors of the list
 * they share.
 *
 * Since 15.7o the students' «درخواست تمدید» come under them, counted in the title too (§15 *The
 * coaching period*): «تمدید» opens the same box from the day after the last day (from today once it
 * is over) and asks before saving, as on «برنامهٔ ثابت»; «رد کردن» asks nothing.
 */
export function TrainerRequestsCard() {
  const students = useStudents();
  const accept = useAcceptTrainerRequest();
  const decline = useDeclineTrainerRequest();
  const acceptRenewal = useAcceptRenewalRequest();
  const declineRenewal = useDeclineRenewalRequest();
  const [accepting, setAccepting] = useState<string | null>(null);
  const [renewing, setRenewing] = useState<string | null>(null);

  const requests = students.data?.requests ?? [];
  const renewals = students.data?.renewalRequests ?? [];
  const count = requests.length + renewals.length;
  const answering =
    accept.isPending || decline.isPending || acceptRenewal.isPending || declineRenewal.isPending;
  const answerFailure =
    accept.error ?? decline.error ?? acceptRenewal.error ?? declineRenewal.error;

  if (count === 0 && answerFailure === null) {
    return null;
  }

  /** One answer's error at a time: a new press clears the others. */
  const resetAll = () => {
    accept.reset();
    decline.reset();
    acceptRenewal.reset();
    declineRenewal.reset();
  };

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          درخواست‌ها{count > 0 && ` (${toPersianDigits(count)})`}
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
              {accepting === request.requestId ? (
                <PeriodDaysForm
                  label="دورهٔ مربی‌گری"
                  startsOn={gymToday()}
                  defaultDays={periodDefaultDays}
                  minDays={periodMinDays}
                  submitLabel="پذیرفتن"
                  pending={accept.isPending}
                  onSubmit={(days) =>
                    accept.mutate(
                      { requestId: request.requestId, days },
                      { onSettled: () => setAccepting(null) },
                    )
                  }
                  onCancel={() => setAccepting(null)}
                />
              ) : (
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    disabled={answering}
                    onClick={() => {
                      resetAll();
                      setRenewing(null);
                      setAccepting(request.requestId);
                    }}
                  >
                    پذیرفتن
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={answering}
                    onClick={() => {
                      resetAll();
                      decline.mutate(request.requestId);
                    }}
                  >
                    رد کردن
                  </Button>
                </div>
              )}
            </li>
          ))}
          {renewals.map((renewal) => (
            <RenewalItem
              key={renewal.requestId}
              renewal={renewal}
              open={renewing === renewal.requestId}
              answering={answering}
              renewPending={acceptRenewal.isPending}
              onOpen={() => {
                resetAll();
                setAccepting(null);
                setRenewing(renewal.requestId);
              }}
              onClose={() => setRenewing(null)}
              onRenew={(days, settled) =>
                acceptRenewal.mutate(
                  { requestId: renewal.requestId, days },
                  {
                    onSettled: settled,
                    onSuccess: () => setRenewing(null),
                  },
                )
              }
              onDecline={() => {
                resetAll();
                declineRenewal.mutate(renewal.requestId);
              }}
            />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

/**
 * One student's «درخواست تمدید»: name and phone, «درخواست تمدید» with where the period stands, and
 * «تمدید» / «رد کردن». «تمدید» opens the days box; the days typed wait for «بله، تمدید» in
 * `PeriodQuestion`, so a slip of the finger is caught (BUSINESS_RULES.md §15).
 */
function RenewalItem({
  renewal,
  open,
  answering,
  renewPending,
  onOpen,
  onClose,
  onRenew,
  onDecline,
}: {
  renewal: RenewalRequestItem;
  open: boolean;
  answering: boolean;
  renewPending: boolean;
  onOpen: () => void;
  onClose: () => void;
  /** `settled` closes the question, whatever the answer. */
  onRenew: (days: number, settled: () => void) => void;
  onDecline: () => void;
}) {
  /** The days typed, waiting for «بله»; `null` while nothing is asked. */
  const [asking, setAsking] = useState<number | null>(null);
  const today = gymToday();
  const startsOn = nextPeriodStart(renewal.periodEndsOn, today);

  return (
    <li className="space-y-2 py-3 first:pt-0 last:pb-0">
      <Person
        fullName={renewal.fullName}
        phoneNumber={renewal.phoneNumber}
        detail={renewalDetail(renewal.periodEndsOn, today)}
      />
      {open ? (
        <PeriodDaysForm
          label="تمدید"
          startsOn={startsOn}
          defaultDays={periodDefaultDays}
          minDays={periodMinDays}
          submitLabel="تمدید"
          pending={renewPending}
          onSubmit={setAsking}
          onCancel={onClose}
        />
      ) : (
        <div className="flex gap-2">
          <Button size="sm" disabled={answering} onClick={onOpen}>
            تمدید
          </Button>
          <Button variant="outline" size="sm" disabled={answering} onClick={onDecline}>
            رد کردن
          </Button>
        </div>
      )}
      {asking !== null && open && (
        <PeriodQuestion
          title="تمدید دوره"
          question={renewQuestion(renewal.fullName, periodEnd(startsOn, asking))}
          confirmLabel="بله، تمدید"
          pending={renewPending}
          onConfirm={() => onRenew(asking, () => setAsking(null))}
          onCancel={() => setAsking(null)}
        />
      )}
    </li>
  );
}

/** «درخواست تمدید، دوره تا ۲۰ آذر ۱۴۰۵», or that it is over. */
function renewalDetail(periodEndsOn: string, today: string): string {
  const state = periodState(periodEndsOn, today);
  return state.kind === "over"
    ? `درخواست تمدید، دوره ${formatDate(periodEndsOn)} تمام شد`
    : `درخواست تمدید، دوره تا ${formatDate(periodEndsOn)}`;
}
