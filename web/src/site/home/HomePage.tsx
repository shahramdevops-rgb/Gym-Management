import { useEffect } from "react";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { errorMessage } from "@/lib/errors";

import { useSiteLogout, useSiteMe } from "../auth/api";
import { MyTrainerCard } from "../coaching/MyTrainerCard";
import { TrainerArea } from "../coaching/TrainerArea";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { SiteFrame } from "../components/SiteFrame";
import { InstallHint } from "../install/InstallHint";
import { MyDebtCard } from "../records/MyDebtCard";
import { MyDetailsCard } from "../records/MyDetailsCard";
import { MyHistoryCard } from "../records/MyHistoryCard";
import { MyPlanCard } from "../records/MyPlanCard";

/** BUSINESS_RULES.md §15 *Trainers*: shown on the sign-in screen after the session is ended. */
export const trainerEndedNotice = "مربی‌گری شما تمام شده است.";

/**
 * The first screen after signing in: the member's area, the trainer's, or both for one person
 * with one phone (BUSINESS_RULES.md §15). The member's plan, debt, trainer, history and details
 * (15.4, 15.5), the trainer's requests and students (15.4), the trainer's timetable from 15.6.
 * This screen also answers the two cases the rules decided: a member deactivated at the desk sees
 * only a notice, and a trainer who was stopped and is not a member is told so and signed out.
 * «خروج» asks first, since signing in again costs an SMS (roadmap 15.5).
 */
export function HomePage() {
  const me = useSiteMe();
  const { mutate: signOut, isPending: signingOut } = useSiteLogout();

  const ended = me.data !== undefined && me.data.member === null && me.data.trainer === null;

  useEffect(() => {
    if (ended) {
      signOut(trainerEndedNotice);
    }
  }, [ended, signOut]);

  if (me.data === undefined || ended) {
    return (
      <SiteFrame>
        {me.isError ? (
          <Alert variant="destructive">{errorMessage(me.error)}</Alert>
        ) : (
          <LoadingState />
        )}
      </SiteFrame>
    );
  }

  const { member, trainer } = me.data;
  const name = member?.fullName ?? trainer?.fullName;

  return (
    <SiteFrame>
      <h1 className="text-center text-xl font-bold">سلام {name}</h1>

      {member !== null &&
        (member.isActive ? (
          <>
            <MyPlanCard />
            <MyDebtCard />
            <MyTrainerCard />
            <MyHistoryCard />
            <MyDetailsCard />
          </>
        ) : (
          <Alert variant="destructive">
            عضویت شما در باشگاه غیرفعال است. برای فعال شدن آن به پذیرش باشگاه مراجعه کنید.
          </Alert>
        ))}

      {trainer !== null && <TrainerArea />}

      <InstallHint />

      <ConfirmDialog
        wide
        label="خروج"
        question="با خروج، برای ورود دوباره یک کد پیامکی تازه لازم است. خارج می‌شوید؟"
        pending={signingOut}
        onConfirm={() => signOut(undefined)}
      />
    </SiteFrame>
  );
}
