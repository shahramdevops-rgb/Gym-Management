import { CheckCircle2, KeyRound } from "lucide-react";
import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import type { Member } from "@/features/members/api";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";

import { useCheckIn, useCheckOut, type Attendance } from "../api";
import { isMissingSubscription, useSellSingleVisit } from "../singleVisit";
import { SingleVisitOffer } from "./SingleVisitOffer";
import { VisitSummary } from "./VisitSummary";

/** What the desk pressed on a member's row. */
export type DeskAction = { kind: "checkIn"; member: Member } | { kind: "checkOut"; member: Member };

type Step =
  | { kind: "confirm" }
  | { kind: "checkedIn"; attendance: Attendance; singleVisit: boolean }
  | { kind: "needsSubscription"; reason: string }
  | { kind: "checkedOut" }
  | { kind: "failed"; reason: string };

interface CheckInOutDialogProps {
  action: DeskAction;
  onClose: () => void;
}

/**
 * One box for the whole of a check-in or check-out (BUSINESS_RULES.md §7 *Confirming at the front
 * desk*): it asks first, then shows the outcome in the same place — the locker large enough to
 * read across the desk, and the debt item by item.
 *
 * Mount it with a `key` per action: each press starts again at "confirm".
 *
 * It closes only with its ✕ or its own buttons. A click outside it does nothing, because the
 * outcome is the part the desk must read, and a stray click is exactly how it would be missed.
 */
export function CheckInOutDialog({ action, onClose }: CheckInOutDialogProps) {
  const { member } = action;
  const [step, setStep] = useState<Step>({ kind: "confirm" });

  const checkIn = useCheckIn();
  const checkOut = useCheckOut();
  const sellSingleVisit = useSellSingleVisit();
  const busy = checkIn.isPending || checkOut.isPending || sellSingleVisit.isPending;

  async function confirmCheckIn() {
    try {
      const attendance = await checkIn.mutateAsync(member.id);
      setStep({ kind: "checkedIn", attendance, singleVisit: false });
    } catch (problem) {
      const reason = errorMessage(problem);
      // The walk-in case the desk meets all day: offer the way in rather than just saying no.
      setStep(
        isMissingSubscription(problem)
          ? { kind: "needsSubscription", reason }
          : { kind: "failed", reason },
      );
    }
  }

  async function confirmCheckOut(attendanceId: string) {
    try {
      await checkOut.mutateAsync(attendanceId);
      setStep({ kind: "checkedOut" });
    } catch (problem) {
      setStep({ kind: "failed", reason: errorMessage(problem) });
    }
  }

  async function sell(planId: string) {
    try {
      const { attendance } = await sellSingleVisit.mutateAsync({ memberId: member.id, planId });
      setStep({ kind: "checkedIn", attendance, singleVisit: true });
    } catch (problem) {
      setStep({ kind: "failed", reason: errorMessage(problem) });
    }
  }

  const visit = member.currentVisit ?? null;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open && !busy) {
          onClose();
        }
      }}
    >
      <DialogContent
        // Nothing is focused on open, so an Enter still held from the search box cannot confirm.
        onOpenAutoFocus={(event) => event.preventDefault()}
        onInteractOutside={(event) => event.preventDefault()}
      >
        {step.kind === "confirm" && action.kind === "checkIn" && (
          <>
            <DialogHeader>
              <DialogTitle>ثبت ورود</DialogTitle>
              <DialogDescription>
                آیا از ثبت ورود <strong className="text-foreground">{member.fullName}</strong> مطمئن
                هستید؟ یک جلسه از اشتراک او کم می‌شود.
              </DialogDescription>
            </DialogHeader>
            <ConfirmButtons
              label="بله، ورود ثبت شود"
              pending={checkIn.isPending}
              onConfirm={() => void confirmCheckIn()}
              onCancel={onClose}
            />
          </>
        )}

        {step.kind === "confirm" && action.kind === "checkOut" && (
          <>
            <DialogHeader>
              <DialogTitle>ثبت خروج</DialogTitle>
              <DialogDescription>
                آیا از ثبت خروج <strong className="text-foreground">{member.fullName}</strong> مطمئن
                هستید؟
              </DialogDescription>
            </DialogHeader>
            {visit === null ? (
              // The row was stale: the member left (or was checked out elsewhere) since the search.
              <Alert variant="destructive">این عضو هم‌اکنون داخل باشگاه نیست.</Alert>
            ) : (
              <>
                <LockerBox number={visit.lockerNumber} returning />
                <VisitSummary memberId={member.id} />
                <ConfirmButtons
                  label="بله، خروج ثبت شود"
                  pending={checkOut.isPending}
                  onConfirm={() => void confirmCheckOut(visit.attendanceId)}
                  onCancel={onClose}
                />
              </>
            )}
          </>
        )}

        {step.kind === "checkedIn" && (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-success">
                <CheckCircle2 className="size-5" aria-hidden />
                {step.singleVisit ? "ورود تک‌جلسه‌ای ثبت شد" : "ورود ثبت شد"}
              </DialogTitle>
              <DialogDescription>{member.fullName}</DialogDescription>
            </DialogHeader>
            <LockerBox number={step.attendance.lockerNumber} />
            <VisitSummary memberId={member.id} />
            <CloseButton onClose={onClose} />
          </>
        )}

        {step.kind === "checkedOut" && (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-success">
                <CheckCircle2 className="size-5" aria-hidden />
                خروج ثبت شد
              </DialogTitle>
              <DialogDescription>{member.fullName}</DialogDescription>
            </DialogHeader>
            {visit !== null && visit.lockerNumber !== null && (
              <LockerBox number={visit.lockerNumber} returning />
            )}
            <CloseButton onClose={onClose} />
          </>
        )}

        {step.kind === "needsSubscription" && (
          <>
            <DialogHeader>
              <DialogTitle>ورود ممکن نیست</DialogTitle>
              <DialogDescription>
                {member.fullName}: {step.reason}
              </DialogDescription>
            </DialogHeader>
            <SingleVisitOffer
              memberId={member.id}
              selling={sellSingleVisit.isPending}
              onSell={(planId) => void sell(planId)}
            />
          </>
        )}

        {step.kind === "failed" && (
          <>
            <DialogHeader>
              <DialogTitle>انجام نشد</DialogTitle>
              <DialogDescription>{member.fullName}</DialogDescription>
            </DialogHeader>
            <Alert variant="destructive">{step.reason}</Alert>
            <CloseButton onClose={onClose} />
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

/**
 * The locker, large enough to read from across the desk. `returning` is the check-out wording:
 * the key comes back rather than going out.
 */
function LockerBox({
  number,
  returning = false,
}: {
  number: number | string | null;
  returning?: boolean;
}) {
  if (number === null) {
    return returning ? null : <Alert role="status">کمد آزادی نبود؛ ورود بدون کمد ثبت شد.</Alert>;
  }

  return (
    <div className="flex items-center justify-between gap-4 rounded-lg border-2 border-primary bg-primary/5 p-4">
      <div className="flex items-center gap-2 text-sm font-medium">
        <KeyRound className="size-5" aria-hidden />
        {returning ? "کلید این کمد را تحویل بگیرید" : "کمد شماره"}
      </div>
      <p
        className="text-5xl leading-none font-bold"
        aria-label={`کمد شماره ${toPersianDigits(number)}`}
      >
        {toPersianDigits(number)}
      </p>
    </div>
  );
}

function ConfirmButtons({
  label,
  pending,
  onConfirm,
  onCancel,
}: {
  label: string;
  pending: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="flex flex-wrap gap-2">
      <Button disabled={pending} onClick={onConfirm}>
        {pending ? "در حال ثبت…" : label}
      </Button>
      <Button variant="outline" disabled={pending} onClick={onCancel}>
        انصراف
      </Button>
    </div>
  );
}

function CloseButton({ onClose }: { onClose: () => void }) {
  return (
    <div className="flex">
      <Button variant="outline" onClick={onClose}>
        بستن
      </Button>
    </div>
  );
}
