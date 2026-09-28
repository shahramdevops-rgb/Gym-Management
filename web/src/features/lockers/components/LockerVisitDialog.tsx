import { ArrowLeftRight, CheckCircle2 } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { SessionsBar } from "@/components/SessionsBar";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useMoveLocker, type CurrentlyInside } from "@/features/attendance/api";
import type { DeskAction } from "@/features/attendance/components/CheckInOutDialog";
import { lowSessionsThreshold } from "@/features/attendance/components/CurrentlyInsideTable";
import { CloseButton, ConfirmButtons, LockerBox } from "@/features/attendance/components/deskParts";
import { VisitSummary } from "@/features/attendance/components/VisitSummary";
import { VisitCafeBox } from "@/features/cafe/components/VisitCafeBox";
import { ServiceChargeBox } from "@/features/serviceCharges/components/ServiceChargeBox";
import { errorMessage } from "@/lib/errors";
import { formatDateTime, toPersianDigits } from "@/lib/format";

import type { Locker } from "../api";
import { LockerMap } from "./LockerMap";

type Step =
  | { kind: "view" }
  | { kind: "pick" }
  | { kind: "confirmMove"; target: Locker }
  | { kind: "moved"; number: Locker["number"] }
  | { kind: "failed"; reason: string };

interface LockerVisitDialogProps {
  /** The open visit on the locker (or reserve place) that was clicked. */
  visit: CurrentlyInside;
  /** Every locker, for choosing where to move the visit. */
  lockers: Locker[];
  /** Check-out and cancel: the page closes this box and opens the confirming one. */
  onDeskAction: (action: DeskAction) => void;
  onClose: () => void;
}

/**
 * One visit, opened from its locker (BUSINESS_RULES.md §7 *Confirming at the front desk*): who it
 * is (linked to their profile), when they came in, the plan and sessions left, the debt item by
 * item, هوازی and cafe for the visit, and check-out, cancel check-in and moving to another locker.
 * A used reserve place opens the same box.
 *
 * Check-out and cancel go through the same confirming box as every other screen, so the key and the
 * debt are handled the same everywhere. Moving picks the target on the map itself, with only free
 * lockers clickable.
 */
export function LockerVisitDialog({
  visit,
  lockers,
  onDeskAction,
  onClose,
}: LockerVisitDialogProps) {
  const [step, setStep] = useState<Step>({ kind: "view" });
  const moveLocker = useMoveLocker();
  const member = { id: visit.memberId, fullName: visit.memberFullName };

  const title =
    visit.lockerNumber === null
      ? "ورود بدون کمد"
      : `کمد شماره ${toPersianDigits(visit.lockerNumber)}`;

  async function confirmMove(target: Locker) {
    try {
      await moveLocker.mutateAsync({ attendanceId: visit.attendanceId, lockerId: target.id });
      setStep({ kind: "moved", number: target.number });
    } catch (problem) {
      setStep({ kind: "failed", reason: errorMessage(problem) });
    }
  }

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open && !moveLocker.isPending) {
          onClose();
        }
      }}
    >
      <DialogContent
        className={step.kind === "pick" ? "max-w-5xl" : "max-w-2xl"}
        onOpenAutoFocus={(event) => event.preventDefault()}
      >
        {step.kind === "view" && (
          <>
            <DialogHeader>
              <DialogTitle>{title}</DialogTitle>
              <DialogDescription>
                <Link
                  to={paths.member(visit.memberId)}
                  className="font-medium text-foreground underline-offset-4 hover:underline"
                >
                  {visit.memberFullName}
                </Link>
                {" · "}ورود: {formatDateTime(visit.checkedInAt)}
              </DialogDescription>
            </DialogHeader>

            <div className="flex items-center gap-3 text-sm">
              <span className="text-muted-foreground">جلسات</span>
              {visit.isSingleSession ? (
                <span className="text-muted-foreground">تک‌جلسه‌ای</span>
              ) : (
                <SessionsBar
                  total={visit.totalSessions}
                  used={visit.usedSessions}
                  remaining={visit.remainingSessions}
                  lowThreshold={lowSessionsThreshold}
                />
              )}
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <p className="text-sm font-medium">هوازی</p>
                <ServiceChargeBox
                  attendanceId={visit.attendanceId}
                  kind="Cardio"
                  charge={visit.serviceCharges.find((charge) => charge.kind === "Cardio")}
                  visitIsOpen
                />
              </div>
              <div className="space-y-1">
                <p className="text-sm font-medium">بوفه</p>
                <VisitCafeBox
                  attendanceId={visit.attendanceId}
                  member={member}
                  orders={visit.cafeOrders ?? []}
                />
              </div>
            </div>

            <VisitSummary memberId={visit.memberId} attendanceId={visit.attendanceId} />

            <div className="flex flex-wrap gap-2 border-t pt-3">
              <Button
                onClick={() =>
                  onDeskAction({
                    kind: "checkOut",
                    member,
                    visit: { attendanceId: visit.attendanceId, lockerNumber: visit.lockerNumber },
                  })
                }
              >
                ثبت خروج
              </Button>
              <Button variant="outline" onClick={() => setStep({ kind: "pick" })}>
                <ArrowLeftRight aria-hidden />
                جابه‌جایی کمد
              </Button>
              <Button
                variant="outline"
                onClick={() =>
                  onDeskAction({ kind: "cancelCheckIn", member, attendanceId: visit.attendanceId })
                }
              >
                لغو ورود
              </Button>
            </div>
          </>
        )}

        {step.kind === "pick" && (
          <>
            <DialogHeader>
              <DialogTitle>جابه‌جایی کمد</DialogTitle>
              <DialogDescription>
                {visit.memberFullName}: کمد آزاد تازه را انتخاب کنید.
              </DialogDescription>
            </DialogHeader>
            <LockerMap
              lockers={lockers}
              mode="pick"
              onSelect={(target) => setStep({ kind: "confirmMove", target })}
            />
            <div className="flex">
              <Button variant="outline" onClick={() => setStep({ kind: "view" })}>
                بازگشت
              </Button>
            </div>
          </>
        )}

        {step.kind === "confirmMove" && (
          <>
            <DialogHeader>
              <DialogTitle>جابه‌جایی کمد</DialogTitle>
              <DialogDescription>
                {visit.memberFullName} از{" "}
                {visit.lockerNumber === null
                  ? "ورود بدون کمد"
                  : `کمد ${toPersianDigits(visit.lockerNumber)}`}{" "}
                به کمد {toPersianDigits(step.target.number)} منتقل شود؟
              </DialogDescription>
            </DialogHeader>
            <ConfirmButtons
              label="بله، منتقل شود"
              pending={moveLocker.isPending}
              onConfirm={() => void confirmMove(step.target)}
              onCancel={() => setStep({ kind: "pick" })}
            />
          </>
        )}

        {step.kind === "moved" && (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-success">
                <CheckCircle2 className="size-5" aria-hidden />
                کمد جابه‌جا شد
              </DialogTitle>
              <DialogDescription>{visit.memberFullName}</DialogDescription>
            </DialogHeader>
            <LockerBox number={step.number} />
            <CloseButton onClose={onClose} />
          </>
        )}

        {step.kind === "failed" && (
          <>
            <DialogHeader>
              <DialogTitle>انجام نشد</DialogTitle>
              <DialogDescription>{visit.memberFullName}</DialogDescription>
            </DialogHeader>
            <Alert variant="destructive">{step.reason}</Alert>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => setStep({ kind: "pick" })}>
                انتخاب کمد دیگر
              </Button>
              <Button variant="outline" onClick={onClose}>
                بستن
              </Button>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
