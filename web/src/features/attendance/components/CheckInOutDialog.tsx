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
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";

import { useCancelCheckIn, useCheckIn, useCheckOut, type Attendance } from "../api";
import { isMissingSubscription, useSellSingleVisit } from "../singleVisit";
import { SingleVisitOffer } from "./SingleVisitOffer";
import { VisitSummary } from "./VisitSummary";

/** Who the box is about. Every screen with these buttons knows at least this much. */
export interface DeskMember {
  id: string;
  fullName: string;
}

/** The visit a check-out closes. */
export interface DeskVisit {
  attendanceId: string;
  /** `null` when no locker was free at check-in: then there is no key to take back. */
  lockerNumber: number | string | null;
}

/**
 * What the desk pressed. A check-out carries the open visit, or `null` when the screen that
 * offered it turned out to be stale (the member already left). Cancelling a check-in carries the
 * visit it undoes.
 */
export type DeskAction =
  | { kind: "checkIn"; member: DeskMember }
  | { kind: "checkOut"; member: DeskMember; visit: DeskVisit | null }
  | { kind: "cancelCheckIn"; member: DeskMember; attendanceId: string };

type Step =
  | { kind: "confirm" }
  | { kind: "checkedIn"; attendance: Attendance; singleVisit: boolean }
  | { kind: "needsSubscription"; reason: string }
  | { kind: "checkedOut" }
  | { kind: "cancelled" }
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
 * Used by every screen with a check-in or check-out button — the entry screen, the member's
 * profile and the "inside" board — through `useDeskDialog`, which also gives each press a fresh
 * box that starts again at "confirm".
 *
 * It closes only with its ✕ or its own buttons. A click outside it does nothing, because the
 * outcome is the part the desk must read, and a stray click is exactly how it would be missed.
 */
export function CheckInOutDialog({ action, onClose }: CheckInOutDialogProps) {
  const { member } = action;
  const [step, setStep] = useState<Step>({ kind: "confirm" });
  const [keyReturned, setKeyReturned] = useState(false);

  const checkIn = useCheckIn();
  const checkOut = useCheckOut();
  const cancelCheckIn = useCancelCheckIn();
  const sellSingleVisit = useSellSingleVisit();
  const busy =
    checkIn.isPending || checkOut.isPending || cancelCheckIn.isPending || sellSingleVisit.isPending;

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

  async function confirmCancel(attendanceId: string) {
    try {
      await cancelCheckIn.mutateAsync(attendanceId);
      setStep({ kind: "cancelled" });
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

  const visit = action.kind === "checkOut" ? action.visit : null;

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
                {visit.lockerNumber !== null && (
                  <KeyReturn
                    number={visit.lockerNumber}
                    returned={keyReturned}
                    onReturnedChange={setKeyReturned}
                  />
                )}
                <VisitSummary memberId={member.id} attendanceId={visit.attendanceId} />
                <ConfirmButtons
                  label="بله، خروج ثبت شود"
                  pending={checkOut.isPending}
                  // With a locker, the key is part of leaving: the button waits for the tick.
                  disabled={visit.lockerNumber !== null && !keyReturned}
                  onConfirm={() => void confirmCheckOut(visit.attendanceId)}
                  onCancel={onClose}
                />
              </>
            )}
          </>
        )}

        {step.kind === "confirm" && action.kind === "cancelCheckIn" && (
          // Only the question: undoing a check-in has nothing for the desk to read, but it gives a
          // session back and frees the locker, so a stray press is worth one more click.
          <>
            <DialogHeader>
              <DialogTitle>لغو ورود</DialogTitle>
              <DialogDescription>
                آیا از لغو ورود <strong className="text-foreground">{member.fullName}</strong> مطمئن
                هستید؟ جلسه به اشتراک او بازمی‌گردد.
              </DialogDescription>
            </DialogHeader>
            <ConfirmButtons
              label="بله، ورود لغو شود"
              pending={cancelCheckIn.isPending}
              onConfirm={() => void confirmCancel(action.attendanceId)}
              onCancel={onClose}
            />
          </>
        )}

        {step.kind === "cancelled" && (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-success">
                <CheckCircle2 className="size-5" aria-hidden />
                ورود لغو شد
              </DialogTitle>
              <DialogDescription>{member.fullName}: جلسه به اشتراک بازگشت.</DialogDescription>
            </DialogHeader>
            <CloseButton onClose={onClose} />
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
              <Alert variant="success" role="status">
                کمد شماره {toPersianDigits(visit.lockerNumber)} آزاد شد.
              </Alert>
            )}
            {/* Again after leaving: this is the last moment to collect what is owed. */}
            <VisitSummary memberId={member.id} attendanceId={visit?.attendanceId} />
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

/** The locker, large enough to read from across the desk. */
function LockerBox({ number }: { number: number | string | null }) {
  if (number === null) {
    return <Alert role="status">کمد آزادی نبود؛ ورود بدون کمد ثبت شد.</Alert>;
  }

  return (
    <div className="flex items-center justify-between gap-4 rounded-lg border-2 border-primary bg-primary/5 p-4">
      <div className="flex items-center gap-2 text-sm font-medium">
        <KeyRound className="size-5" aria-hidden />
        کمد شماره
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

/**
 * The key, before check-out frees the locker (BUSINESS_RULES.md §7 *Confirming at the front
 * desk*). Once the visit is closed the locker is handed to the next person in, so a key still in
 * someone's pocket is a problem for them, not for the member who took it. The tick is the reminder
 * the desk cannot skip past.
 */
function KeyReturn({
  number,
  returned,
  onReturnedChange,
}: {
  number: number | string;
  returned: boolean;
  onReturnedChange: (returned: boolean) => void;
}) {
  return (
    <div className="space-y-3 rounded-lg border-2 border-primary bg-primary/5 p-4">
      <div className="flex items-center justify-between gap-4">
        <p className="flex items-center gap-2 text-lg font-bold">
          <KeyRound className="size-6" aria-hidden />
          کلید کمد را از عضو تحویل بگیرید
        </p>
        <p
          className="text-5xl leading-none font-bold"
          aria-label={`کمد شماره ${toPersianDigits(number)}`}
        >
          {toPersianDigits(number)}
        </p>
      </div>
      <label className="flex cursor-pointer items-center gap-2 text-base font-medium">
        <input
          type="checkbox"
          className="size-5 accent-primary"
          checked={returned}
          onChange={(event) => onReturnedChange(event.target.checked)}
        />
        کلید کمد شماره {toPersianDigits(number)} را تحویل گرفتم
      </label>
    </div>
  );
}

function ConfirmButtons({
  label,
  pending,
  disabled = false,
  onConfirm,
  onCancel,
}: {
  label: string;
  pending: boolean;
  disabled?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="flex flex-wrap gap-2">
      <Button disabled={pending || disabled} onClick={onConfirm}>
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
