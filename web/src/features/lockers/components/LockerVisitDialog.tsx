import { ArrowLeftRight, CheckCircle2, History } from "lucide-react";
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
import { lowSessionsThreshold } from "@/features/attendance/renewal";
import { CloseButton, ConfirmButtons, LockerBox } from "@/features/attendance/components/deskParts";
import { GuestSettleForm } from "@/features/attendance/components/GuestSettleForm";
import { VisitSummary } from "@/features/attendance/components/VisitSummary";
import { cardioOnlyLabel, guestLabel, holderOf } from "@/features/attendance/holder";
import { VisitCafeBox } from "@/features/cafe/components/VisitCafeBox";
import { MiscellaneousSaleBox } from "@/features/serviceCharges/components/MiscellaneousSaleBox";
import { ServiceChargeBox } from "@/features/serviceCharges/components/ServiceChargeBox";
import { errorMessage } from "@/lib/errors";
import { formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";
import { addMoney, isPositiveMoney } from "@/lib/money";

import type { Locker } from "../api";
import { LockerMap } from "./LockerMap";
import { LockerTodayHistory } from "./LockerTodayHistory";

type Step =
  | { kind: "view" }
  /** Who had the locker today (BUSINESS_RULES.md §6); «بازگشت» goes back to the visit. */
  | { kind: "history"; lockerId: string }
  | { kind: "pick" }
  /** «تسویه یکجا» for a guest: every unpaid cafe order of the visit (BUSINESS_RULES.md §7 *Guest visit*). */
  | { kind: "settle" }
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
 * is (linked to their profile), when they came in, the sessions beside them, the plan and its
 * dates, the debt item by item, هوازی, cafe and متفرقه for the visit, and check-out, cancel check-in, moving
 * to another locker and who had the locker earlier today. A used reserve place opens the same box,
 * without the locker's history.
 *
 * A cardio-only visit (BUSINESS_RULES.md §7 *Cardio-only visit*) is marked «فقط هوازی», and its
 * check-out stays disabled, with the reason said, until a هوازی amount is recorded.
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
  const member = holderOf(visit);
  // Null for a guest (BUSINESS_RULES.md §7 *Guest visit*), whose box has no profile, sessions or هوازی.
  const memberId = visit.memberId;
  // Null on a reserve place, which has no history of its own.
  const lockerId = visit.lockerId;
  // The visit's standing هوازی (voided charges are never listed). A cardio-only visit cannot be
  // checked out without one (BUSINESS_RULES.md §7 *Cardio-only visit*); the API refuses it too.
  const cardioCharge = visit.serviceCharges.find((charge) => charge.kind === "Cardio");
  const cardioMissing = visit.isCardioOnly && cardioCharge === undefined;

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
        {step.kind === "view" && memberId === null && (
          <GuestView
            visit={visit}
            title={title}
            onSettle={() => setStep({ kind: "settle" })}
            onCheckOut={() =>
              onDeskAction({
                kind: "checkOut",
                member,
                visit: { attendanceId: visit.attendanceId, lockerNumber: visit.lockerNumber },
              })
            }
            onMove={() => setStep({ kind: "pick" })}
            onCancelCheckIn={() =>
              onDeskAction({ kind: "cancelCheckIn", member, attendanceId: visit.attendanceId })
            }
            onHistory={lockerId === null ? null : () => setStep({ kind: "history", lockerId })}
          />
        )}

        {step.kind === "settle" && (
          <>
            <DialogHeader>
              <DialogTitle>تسویه یکجا — {member.fullName}</DialogTitle>
              <DialogDescription>
                همهٔ خریدهای پرداخت‌نشدهٔ بوفهٔ این مهمان یکجا پرداخت می‌شود.
              </DialogDescription>
            </DialogHeader>
            <GuestSettleForm
              attendanceId={visit.attendanceId}
              outstanding={guestOutstanding(visit)}
              onDone={() => setStep({ kind: "view" })}
              onCancel={() => setStep({ kind: "view" })}
            />
          </>
        )}

        {step.kind === "view" && memberId !== null && (
          <>
            {/* The sessions sit beside the name, where the header had room to spare; pe-6 keeps them
                clear of the ✕. */}
            <div className="flex flex-wrap items-start justify-between gap-4 pe-6">
              <DialogHeader>
                <DialogTitle>{title}</DialogTitle>
                <DialogDescription>
                  <Link
                    to={paths.member(memberId)}
                    className="font-medium text-foreground underline-offset-4 hover:underline"
                  >
                    {member.fullName}
                  </Link>
                  {visit.isCardioOnly && (
                    <>
                      {" · "}
                      <span className="rounded bg-cardio/25 px-1.5 py-0.5 text-xs font-medium text-foreground">
                        {cardioOnlyLabel}
                      </span>
                    </>
                  )}
                  {" · "}ورود: {formatDateTime(visit.checkedInAt)}
                </DialogDescription>
              </DialogHeader>
              <VisitSessions visit={visit} />
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <div className="space-y-1">
                <p className="text-sm font-medium">هوازی</p>
                <ServiceChargeBox
                  attendanceId={visit.attendanceId}
                  kind="Cardio"
                  charge={cardioCharge}
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
              <div className="space-y-1">
                <p className="text-sm font-medium">متفرقه</p>
                <MiscellaneousSaleBox
                  attendanceId={visit.attendanceId}
                  memberName={member.fullName}
                  sales={visit.serviceCharges.filter((charge) => charge.kind === "Miscellaneous")}
                />
              </div>
            </div>

            {/* Without its own sessions line: the header already shows them. */}
            <VisitSummary
              memberId={memberId}
              attendanceId={visit.attendanceId}
              withSessions={false}
            />

            {cardioMissing && (
              <Alert role="status">
                ورود فقط هوازی است: خروج پس از ثبت مبلغ هوازی ممکن است (پرداخت‌نشده هم بدهی عضو
                می‌شود).
              </Alert>
            )}
            <div className="flex flex-wrap gap-2 border-t pt-3">
              <Button
                disabled={cardioMissing}
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
              {lockerId !== null && (
                <Button variant="outline" onClick={() => setStep({ kind: "history", lockerId })}>
                  <History aria-hidden />
                  تاریخچه امروز این کمد
                </Button>
              )}
            </div>
          </>
        )}

        {step.kind === "history" && (
          <>
            <DialogHeader>
              <DialogTitle>{title} — تاریخچه امروز</DialogTitle>
              <DialogDescription>
                کسانی که امروز این کمد را داشته‌اند، از اولین نفر.
              </DialogDescription>
            </DialogHeader>
            <LockerTodayHistory lockerId={step.lockerId} />
            <div className="flex">
              <Button variant="outline" onClick={() => setStep({ kind: "view" })}>
                بازگشت
              </Button>
            </div>
          </>
        )}

        {step.kind === "pick" && (
          <>
            <DialogHeader>
              <DialogTitle>جابه‌جایی کمد</DialogTitle>
              <DialogDescription>
                {member.fullName}: کمد آزاد تازه را انتخاب کنید.
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
                {member.fullName} از{" "}
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
              <DialogDescription>{member.fullName}</DialogDescription>
            </DialogHeader>
            <LockerBox number={step.number} />
            <CloseButton onClose={onClose} />
          </>
        )}

        {step.kind === "failed" && (
          <>
            <DialogHeader>
              <DialogTitle>انجام نشد</DialogTitle>
              <DialogDescription>{member.fullName}</DialogDescription>
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

/** What a guest's visit still owes the cafe: what «تسویه یکجا» pays and what blocks check-out. */
function guestOutstanding(visit: CurrentlyInside): string {
  return addMoney(...(visit.cafeOrders ?? []).map((order) => order.outstanding));
}

interface GuestViewProps {
  visit: CurrentlyInside;
  title: string;
  onSettle: () => void;
  onCheckOut: () => void;
  onMove: () => void;
  onCancelCheckIn: () => void;
  /** `null` on a reserve place, which has no history of its own. */
  onHistory: (() => void) | null;
}

/**
 * A guest's box (BUSINESS_RULES.md §7 *Guest visit*): the name and «مهمان», with no profile link,
 * no sessions and no هوازی. The cafe is as for a member, plus «تسویه یکجا» while anything is
 * unpaid, and check-out waits until it is paid: a guest has no account to leave a debt on. Move
 * and cancel work as for a member.
 */
function GuestView({
  visit,
  title,
  onSettle,
  onCheckOut,
  onMove,
  onCancelCheckIn,
  onHistory,
}: GuestViewProps) {
  const guest = holderOf(visit);
  const outstanding = guestOutstanding(visit);
  const owes = isPositiveMoney(outstanding);

  return (
    <>
      <DialogHeader className="pe-6">
        <DialogTitle>{title}</DialogTitle>
        <DialogDescription>
          <span className="font-medium text-foreground">{guest.fullName}</span>
          {" · "}
          <span className="rounded bg-guest/15 px-1.5 py-0.5 text-xs font-medium text-foreground">
            {guestLabel}
          </span>
          {" · "}ورود: {formatDateTime(visit.checkedInAt)}
        </DialogDescription>
      </DialogHeader>

      <div className="space-y-2">
        <p className="text-sm font-medium">بوفه</p>
        <div className="flex flex-wrap items-center gap-3">
          <VisitCafeBox
            attendanceId={visit.attendanceId}
            member={guest}
            orders={visit.cafeOrders ?? []}
          />
          {owes && (
            <Button size="sm" onClick={onSettle}>
              تسویه یکجا ({formatMoney(outstanding)})
            </Button>
          )}
        </div>
      </div>

      <div className="space-y-2 border-t pt-3">
        {owes && (
          <Alert role="status">مهمان حسابی ندارد: خروج پس از پرداخت خریدهای بوفه ثبت می‌شود.</Alert>
        )}
        <div className="flex flex-wrap gap-2">
          <Button disabled={owes} onClick={onCheckOut}>
            ثبت خروج
          </Button>
          <Button variant="outline" onClick={onMove}>
            <ArrowLeftRight aria-hidden />
            جابه‌جایی کمد
          </Button>
          <Button variant="outline" onClick={onCancelCheckIn}>
            لغو ورود
          </Button>
          {onHistory !== null && (
            <Button variant="outline" onClick={onHistory}>
              <History aria-hidden />
              تاریخچه امروز این کمد
            </Button>
          )}
        </div>
      </div>
    </>
  );
}

/**
 * The visit's sessions, used of total over a bar, and how many that leaves: the one count the desk
 * reads at the door. A single visit has nothing to count.
 */
function VisitSessions({ visit }: { visit: CurrentlyInside }) {
  return (
    <section aria-label="جلسات" className="w-full space-y-1 text-sm sm:w-48">
      <p className="text-muted-foreground">جلسات</p>
      {visit.isSingleSession ? (
        <p className="font-medium">تک‌جلسه‌ای</p>
      ) : (
        <>
          <SessionsBar
            className="w-full"
            total={visit.totalSessions ?? 0}
            used={visit.usedSessions ?? 0}
            remaining={visit.remainingSessions ?? 0}
            lowThreshold={lowSessionsThreshold}
          />
          <p className="text-xs text-muted-foreground">
            {toPersianDigits(visit.remainingSessions ?? 0)} جلسه مانده
          </p>
        </>
      )}
    </section>
  );
}
