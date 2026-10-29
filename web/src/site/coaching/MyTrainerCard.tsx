import { Link } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate } from "@/lib/format";

import { ConfirmDialog } from "../components/ConfirmDialog";
import { sitePaths } from "../paths";
import { useCancelTrainerRequest, useEndMyTrainer, useMyTrainer } from "./api";

/**
 * «مربی من» on an active member's home (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap
 * 15.4b): the trainer now, the request waiting, both while the member changes trainer (the old one
 * stays until the new one accepts), or neither. Picking is on its own page; ending asks first.
 */
export function MyTrainerCard() {
  const myTrainer = useMyTrainer();
  const cancel = useCancelTrainerRequest();
  const end = useEndMyTrainer();

  const failure = cancel.error ?? end.error;

  return (
    <Card accent={false} className="shadow-raised">
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
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="font-semibold">{myTrainer.data.trainer.fullName}</p>
                  <p className="text-sm text-muted-foreground">
                    از {formatDate(myTrainer.data.trainer.since)}
                  </p>
                </div>
                <ConfirmDialog
                  label="پایان مربی‌گری"
                  question={`مربی‌گری ${myTrainer.data.trainer.fullName} برای شما از امروز تمام می‌شود. ادامه می‌دهید؟`}
                  pending={end.isPending}
                  onConfirm={(close) => end.mutate(undefined, { onSettled: close })}
                />
              </div>
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

            <Button asChild variant="secondary" className="w-full">
              <Link to={sitePaths.trainers}>
                {myTrainer.data.trainer === null ? "انتخاب مربی" : "تغییر مربی"}
              </Link>
            </Button>
          </>
        )}
      </CardContent>
    </Card>
  );
}
