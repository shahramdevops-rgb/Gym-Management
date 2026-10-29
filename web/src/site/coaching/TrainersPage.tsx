import { ArrowRight, Users } from "lucide-react";
import { Link, Navigate, useNavigate } from "react-router";

import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";

import { useSiteMe } from "../auth/api";
import { SiteFrame } from "../components/SiteFrame";
import { sitePaths } from "../paths";
import { useMyTrainer, useRequestTrainer, useTrainerChoices } from "./api";

/**
 * The trainers a member can pick (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap
 * 15.4b), by name only until 14.1's photos. «انتخاب این مربی» asks nothing: a waiting request can
 * be taken back with «لغو». The trainer now and the one already asked are marked and cannot be
 * pressed, since pressing the same trainer again makes no second request.
 */
export function TrainersPage() {
  const me = useSiteMe();
  const trainers = useTrainerChoices();
  const myTrainer = useMyTrainer();
  const request = useRequestTrainer();
  const navigate = useNavigate();

  // Only an active member picks a trainer; anyone else reached this address by typing it.
  if (me.data !== undefined && me.data.member?.isActive !== true) {
    return <Navigate to={sitePaths.home} replace />;
  }

  const current = myTrainer.data?.trainer ?? null;
  const asked = myTrainer.data?.request ?? null;
  const failure = trainers.error ?? myTrainer.error;

  return (
    <SiteFrame>
      <div className="flex items-center gap-2">
        <Button asChild variant="ghost" size="sm">
          <Link to={sitePaths.home} aria-label="بازگشت">
            <ArrowRight aria-hidden />
          </Link>
        </Button>
        <h1 className="text-xl font-bold">انتخاب مربی</h1>
      </div>

      {current !== null && (
        <p className="text-sm text-muted-foreground">
          تا پذیرفتن مربی تازه، {current.fullName} مربی شما می‌ماند.
        </p>
      )}

      {request.isError && <Alert variant="destructive">{errorMessage(request.error)}</Alert>}

      {trainers.data === undefined || myTrainer.data === undefined ? (
        failure !== null ? (
          <Alert variant="destructive">{errorMessage(failure)}</Alert>
        ) : (
          <LoadingState />
        )
      ) : trainers.data.length === 0 ? (
        <EmptyState icon={Users}>فعلاً مربی‌ای برای انتخاب نیست.</EmptyState>
      ) : (
        <ul aria-label="مربیان" className="space-y-2">
          {trainers.data.map((trainer) => {
            const isCurrent = trainer.trainerId === current?.trainerId;
            const isAsked = trainer.trainerId === asked?.trainerId;

            return (
              <li
                key={trainer.trainerId}
                className="flex items-center justify-between gap-2 rounded-xl border bg-card p-3 shadow-raised"
              >
                <span className="font-semibold">{trainer.fullName}</span>
                {isCurrent ? (
                  <Badge variant="success">مربی شما</Badge>
                ) : isAsked ? (
                  <Badge variant="warning">در انتظار پاسخ</Badge>
                ) : (
                  <Button
                    size="sm"
                    disabled={request.isPending}
                    onClick={() =>
                      request.mutate(trainer.trainerId, {
                        onSuccess: () => void navigate(sitePaths.home),
                      })
                    }
                  >
                    انتخاب این مربی
                  </Button>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </SiteFrame>
  );
}
