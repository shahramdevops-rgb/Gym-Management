import { Link } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate, gymToday } from "@/lib/format";

import { ConfirmDialog, quietButtonClassName } from "../components/ConfirmDialog";
import { sitePaths } from "../paths";
import {
  useCancelRenewalRequest,
  useCancelTrainerRequest,
  useEndMyTrainer,
  useMyTrainer,
  useRequestRenewal,
  type MyTrainer,
} from "./api";
import { periodLine, periodState } from "./period";

/**
 * «مربی من» on an active member's home (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap
 * 15.4b): the trainer now, the request waiting, both while the member changes trainer (the old one
 * stays until the new one accepts), or neither. Picking is on its own page; ending asks first.
 * The coaching period's last day and the days left show under the trainer's name (15.7h); a link
 * from before it shows nothing until the trainer gives one.
 *
 * With a trainer, «تغییر مربی» and «پایان مربی‌گری» are small grey text at the card's foot (the
 * developer, 1405/08/20: "not so much in sight"), as the trainer's «پایان» left their home (15.7g);
 * without one, «انتخاب مربی» is the card's own button.
 *
 * «درخواست تمدید» (15.7o) sits under the period's line: the button from the period's last days and
 * after it is over, the request waiting with «لغو درخواست», or the trainer's decline while the
 * coaching still ends on the day asked about (`RenewalPart`).
 */
export function MyTrainerCard() {
  const myTrainer = useMyTrainer();
  const cancel = useCancelTrainerRequest();
  const end = useEndMyTrainer();
  const askRenewal = useRequestRenewal();
  const cancelRenewal = useCancelRenewalRequest();

  const failure = cancel.error ?? end.error ?? askRenewal.error ?? cancelRenewal.error;

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          مربی من
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {myTrainer.data === undefined ? (
          myTrainer.isError ? (
            <Alert variant="destructive">{errorMessage(myTrainer.error)}</Alert>
          ) : (
            <LoadingState className="py-2" />
          )
        ) : (
          <>
            {myTrainer.data.trainer === null ? (
              <p className="text-sm text-muted-foreground">هنوز مربی ندارید.</p>
            ) : (
              <div>
                <p className="font-semibold">{myTrainer.data.trainer.fullName}</p>
                <p className="text-sm text-muted-foreground">
                  از {formatDate(myTrainer.data.trainer.since)}
                </p>
                {myTrainer.data.trainer.periodEndsOn !== null && (
                  <p className="text-sm text-muted-foreground">
                    {periodLine(periodState(myTrainer.data.trainer.periodEndsOn, gymToday()))}
                  </p>
                )}
              </div>
            )}

            {myTrainer.data.trainer !== null && (
              <RenewalPart
                trainer={myTrainer.data.trainer}
                pending={askRenewal.isPending || cancelRenewal.isPending}
                onAsk={() => {
                  cancelRenewal.reset();
                  askRenewal.mutate(undefined);
                }}
                onCancel={() => {
                  askRenewal.reset();
                  cancelRenewal.mutate(undefined);
                }}
              />
            )}

            {myTrainer.data.request !== null && (
              <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-dashed p-3">
                <p className="text-sm">
                  درخواست شما برای{" "}
                  <strong className="font-semibold">{myTrainer.data.request.fullName}</strong> در
                  انتظار پاسخ است.
                </p>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={cancel.isPending}
                  onClick={() => cancel.mutate(undefined)}
                >
                  لغو
                </Button>
              </div>
            )}

            {failure !== null && <Alert variant="destructive">{errorMessage(failure)}</Alert>}

            {myTrainer.data.trainer === null ? (
              <Button asChild variant="secondary" className="w-full">
                <Link to={sitePaths.trainers}>انتخاب مربی</Link>
              </Button>
            ) : (
              <div className="flex flex-wrap justify-end gap-x-1 border-t pt-2">
                <Button asChild variant="ghost" size="sm" className={quietButtonClassName}>
                  <Link to={sitePaths.trainers}>تغییر مربی</Link>
                </Button>
                <ConfirmDialog
                  quiet
                  label="پایان مربی‌گری"
                  question={`مربی‌گری ${myTrainer.data.trainer.fullName} برای شما از امروز تمام می‌شود. ادامه می‌دهید؟`}
                  pending={end.isPending}
                  onConfirm={(close) => end.mutate(undefined, { onSettled: close })}
                />
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * «درخواست تمدید» (BUSINESS_RULES.md §15 *The coaching period*, roadmap 15.7o). The API says what
 * shows: `renewal` (waiting, or declined while the last day is the one asked about) and
 * `canAskRenewal` (the period's last days or after it is over, nothing waiting). Asking asks nothing
 * first: it costs no SMS and can be taken back.
 */
function RenewalPart({
  trainer,
  pending,
  onAsk,
  onCancel,
}: {
  trainer: NonNullable<MyTrainer["trainer"]>;
  pending: boolean;
  onAsk: () => void;
  onCancel: () => void;
}) {
  if (trainer.renewal === "Pending") {
    return (
      <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-dashed p-3">
        <p className="text-sm">درخواست تمدید در انتظار پاسخ مربی</p>
        <Button variant="outline" size="sm" disabled={pending} onClick={onCancel}>
          لغو درخواست
        </Button>
      </div>
    );
  }

  if (!trainer.canAskRenewal && trainer.renewal !== "Declined") {
    return null;
  }

  return (
    <div className="space-y-2">
      {trainer.renewal === "Declined" && (
        <p className="text-sm text-warning-ink">درخواست تمدید رد شد</p>
      )}
      {trainer.canAskRenewal && (
        <Button variant="secondary" className="w-full" disabled={pending} onClick={onAsk}>
          درخواست تمدید
        </Button>
      )}
    </div>
  );
}
