import { useEffect } from "react";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";

import { useSiteLogout, useSiteMe } from "../auth/api";
import { SiteFrame } from "../components/SiteFrame";
import { InstallHint } from "../install/InstallHint";

/** BUSINESS_RULES.md §15 *Trainers*: shown on the sign-in screen after the session is ended. */
export const trainerEndedNotice = "مربی‌گری شما تمام شده است.";

/**
 * The first screen after signing in: the member's area, the trainer's, or both for one person
 * with one phone (BUSINESS_RULES.md §15). Their contents come with their own tasks («برنامه من»
 * in 15.5, the trainer's tools from 15.6); this screen already answers the two cases the rules
 * decided: a member deactivated at the desk sees only a notice, and a trainer who was stopped and
 * is not a member is told so and signed out.
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
          <Card accent={false} className="shadow-raised">
            <CardHeader>
              <CardTitle role="heading" aria-level={2}>
                عضویت من
              </CardTitle>
              <CardDescription>
                جلسه‌های باقی‌مانده، پایان دوره و مراجعه‌های شما به‌زودی اینجا نشان داده می‌شود.
              </CardDescription>
            </CardHeader>
          </Card>
        ) : (
          <Alert variant="destructive">
            عضویت شما در باشگاه غیرفعال است. برای فعال شدن آن به پذیرش باشگاه مراجعه کنید.
          </Alert>
        ))}

      {trainer !== null && (
        <Card accent={false} className="shadow-raised">
          <CardHeader>
            <CardTitle role="heading" aria-level={2}>
              بخش مربی
            </CardTitle>
            <CardDescription>
              شاگردان، برنامهٔ سانس‌ها و حضور و غیاب به‌زودی اینجا می‌آید.
            </CardDescription>
          </CardHeader>
        </Card>
      )}

      <InstallHint />

      <Button
        variant="outline"
        className="w-full"
        disabled={signingOut}
        onClick={() => signOut(undefined)}
      >
        خروج
      </Button>
    </SiteFrame>
  );
}
