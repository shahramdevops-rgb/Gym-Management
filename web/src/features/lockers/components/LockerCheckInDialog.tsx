import { CheckCircle2, Search, UserPlus } from "lucide-react";
import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useCheckIn, type Attendance } from "@/features/attendance/api";
import type { DeskMember } from "@/features/attendance/components/CheckInOutDialog";
import { CloseButton, ConfirmButtons, LockerBox } from "@/features/attendance/components/deskParts";
import { SingleVisitOffer } from "@/features/attendance/components/SingleVisitOffer";
import { VisitSummary } from "@/features/attendance/components/VisitSummary";
import { isMissingSubscription, useSellSingleVisit } from "@/features/attendance/singleVisit";
import { useCreateMember, useMemberList, type Member } from "@/features/members/api";
import { MemberForm } from "@/features/members/components/MemberForm";
import { searchMinLength } from "@/features/members/schemas";
import { memberDraftFromSearch } from "@/features/members/searchDraft";
import { errorMessage } from "@/lib/errors";
import { formatPhone, toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";
import { useDebouncedCallback } from "@/lib/useDebouncedCallback";

import { useSetLockerOutOfService, type Locker } from "../api";

/** Long enough to skip the keys of one word, short enough to feel immediate (the same as the search screen). */
const searchDelayMs = 300;

/** How many matches the box lists; a longer list belongs on the search screen. */
const shownMatches = 8;

/** Where the visit goes: the locker the desk clicked, or a reserve place (BUSINESS_RULES.md §6). */
export type CheckInPlace = { kind: "locker"; locker: Locker } | { kind: "reserve" };

type Step =
  | { kind: "search" }
  | { kind: "register"; text: string }
  | { kind: "confirm"; member: DeskMember }
  | { kind: "checkedIn"; member: DeskMember; attendance: Attendance; singleVisit: boolean }
  | { kind: "needsSubscription"; member: DeskMember; reason: string }
  | { kind: "failed"; reason: string };

interface LockerCheckInDialogProps {
  place: CheckInPlace;
  onClose: () => void;
}

/**
 * The whole check-in, from the locker the desk clicked (BUSINESS_RULES.md §7 *Confirming at the
 * front desk*): find the member by name or mobile, confirm, and the visit is recorded with that
 * locker. Everything the search screen used to offer for check-in lives here now: registering a
 * person who is not found, and selling a single visit when the member has nothing usable (§4) —
 * checked in with this same locker.
 *
 * A member who is already inside is marked in the results, and choosing them shows why not and
 * sends nothing; the API would refuse it anyway (`Attendance.AlreadyCheckedIn`).
 *
 * Like the check-out box, it closes only with its ✕ or its own buttons: the outcome is what the
 * desk must read.
 */
export function LockerCheckInDialog({ place, onClose }: LockerCheckInDialogProps) {
  const [step, setStep] = useState<Step>({ kind: "search" });
  const lockerId = place.kind === "locker" ? place.locker.id : null;

  const checkIn = useCheckIn();
  const sellSingleVisit = useSellSingleVisit();
  const createMember = useCreateMember();
  const setOutOfService = useSetLockerOutOfService();
  const [outOfServiceError, setOutOfServiceError] = useState<string | null>(null);
  const busy = checkIn.isPending || sellSingleVisit.isPending || setOutOfService.isPending;

  const title =
    place.kind === "locker" ? `کمد شماره ${toPersianDigits(place.locker.number)}` : "ورود بدون کمد";

  async function confirmCheckIn(member: DeskMember) {
    try {
      const attendance = await checkIn.mutateAsync({ memberId: member.id, lockerId });
      setStep({ kind: "checkedIn", member, attendance, singleVisit: false });
    } catch (problem) {
      const reason = errorMessage(problem);
      // The walk-in case the desk meets all day: offer the way in rather than just saying no.
      setStep(
        isMissingSubscription(problem)
          ? { kind: "needsSubscription", member, reason }
          : { kind: "failed", reason },
      );
    }
  }

  async function sell(member: DeskMember) {
    try {
      const { attendance } = await sellSingleVisit.mutateAsync({
        memberId: member.id,
        lockerId,
      });
      setStep({ kind: "checkedIn", member, attendance, singleVisit: true });
    } catch (problem) {
      setStep({ kind: "failed", reason: errorMessage(problem) });
    }
  }

  async function takeOutOfService(locker: Locker) {
    setOutOfServiceError(null);
    try {
      await setOutOfService.mutateAsync({ id: locker.id, outOfService: true });
      onClose();
    } catch (problem) {
      setOutOfServiceError(errorMessage(problem));
    }
  }

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
        onOpenAutoFocus={(event) => {
          // Straight into the search box on open; nowhere else, so a held Enter confirms nothing.
          if (step.kind !== "search") {
            event.preventDefault();
          }
        }}
        onInteractOutside={(event) => event.preventDefault()}
      >
        {step.kind === "search" && (
          <>
            <DialogHeader>
              <DialogTitle>{title}</DialogTitle>
              <DialogDescription>عضو را با نام یا شماره موبایل پیدا کنید.</DialogDescription>
            </DialogHeader>
            <MemberSearch
              onChoose={(member) => setStep({ kind: "confirm", member })}
              onRegister={(text) => setStep({ kind: "register", text })}
            />
            {place.kind === "locker" && (
              <div className="space-y-2 border-t pt-3">
                {outOfServiceError !== null && (
                  <Alert variant="destructive">{outOfServiceError}</Alert>
                )}
                <Button
                  size="sm"
                  variant="ghost"
                  disabled={setOutOfService.isPending}
                  onClick={() => void takeOutOfService(place.locker)}
                >
                  خارج از سرویس کردن این کمد
                </Button>
              </div>
            )}
          </>
        )}

        {step.kind === "register" && (
          // Registering here rather than on another screen: the person is standing at the desk, and
          // the next step is letting them in (roadmap 6.5.4). They have no subscription by
          // definition, so the check-in's refusal offers the single visit like any other.
          <>
            <DialogHeader>
              <DialogTitle>{title} — عضو جدید</DialogTitle>
            </DialogHeader>
            <MemberForm
              // The search already holds the name or the phone; the desk should not type it again.
              defaultValues={memberDraftFromSearch(step.text)}
              submitLabel="ثبت و ادامه"
              submittingLabel="در حال ثبت…"
              onSubmit={async (input) => {
                const member = await createMember.mutateAsync(input);
                setStep({ kind: "confirm", member });
              }}
              actions={
                <Button type="button" variant="ghost" onClick={() => setStep({ kind: "search" })}>
                  بازگشت
                </Button>
              }
            />
          </>
        )}

        {step.kind === "confirm" && (
          <>
            <DialogHeader>
              <DialogTitle>ثبت ورود</DialogTitle>
              <DialogDescription>
                آیا از ثبت ورود <strong className="text-foreground">{step.member.fullName}</strong>{" "}
                {place.kind === "locker"
                  ? `با کمد شماره ${toPersianDigits(place.locker.number)}`
                  : "بدون کمد"}{" "}
                مطمئن هستید؟ یک جلسه از اشتراک او کم می‌شود.
              </DialogDescription>
            </DialogHeader>
            <ConfirmButtons
              label="بله، ورود ثبت شود"
              pending={checkIn.isPending}
              onConfirm={() => void confirmCheckIn(step.member)}
              onCancel={() => setStep({ kind: "search" })}
            />
          </>
        )}

        {step.kind === "checkedIn" && (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-success">
                <CheckCircle2 className="size-5" aria-hidden />
                {step.singleVisit ? "ورود تک‌جلسه‌ای ثبت شد" : "ورود ثبت شد"}
              </DialogTitle>
              <DialogDescription>{step.member.fullName}</DialogDescription>
            </DialogHeader>
            <LockerBox number={step.attendance.lockerNumber} />
            <VisitSummary memberId={step.member.id} />
            <CloseButton onClose={onClose} />
          </>
        )}

        {step.kind === "needsSubscription" && (
          <>
            <DialogHeader>
              <DialogTitle>ورود ممکن نیست</DialogTitle>
              <DialogDescription>
                {step.member.fullName}: {step.reason}
              </DialogDescription>
            </DialogHeader>
            <SingleVisitOffer
              memberId={step.member.id}
              selling={sellSingleVisit.isPending}
              onSell={() => void sell(step.member)}
            />
          </>
        )}

        {step.kind === "failed" && (
          <>
            <DialogHeader>
              <DialogTitle>انجام نشد</DialogTitle>
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
 * One box for a name or a mobile number, as on the search screen, with the matches listed below
 * it. A member already inside is marked with their locker and cannot be chosen.
 */
function MemberSearch({
  onChoose,
  onRegister,
}: {
  onChoose: (member: Member) => void;
  onRegister: (text: string) => void;
}) {
  const [text, setText] = useState("");
  const [search, setSearch] = useState("");
  const [insideError, setInsideError] = useState<string | null>(null);
  const debouncedSearch = useDebouncedCallback(
    (value: string) => setSearch(normalizeInput(value)),
    searchDelayMs,
  );

  const ready = search.length >= searchMinLength;
  const results = useMemberList({ search, page: 1 }, { enabled: ready });

  function choose(member: Member) {
    const visit = member.currentVisit ?? null;
    if (visit !== null) {
      // BUSINESS_RULES.md §7: no second open visit. Said here, before anything is sent.
      setInsideError(`${member.fullName} هم‌اکنون داخل باشگاه است (${placeOf(visit)}).`);
      return;
    }
    onChoose(member);
  }

  return (
    <div className="space-y-3">
      <div className="relative">
        <Search
          className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          aria-hidden
        />
        <Input
          type="search"
          aria-label="نام یا شماره موبایل"
          placeholder="نام یا شماره موبایل عضو…"
          className="h-11 ps-9 text-base"
          autoFocus
          value={text}
          onChange={(event) => {
            setText(event.target.value);
            setInsideError(null);
            debouncedSearch.run(event.target.value);
          }}
        />
      </div>

      {insideError !== null && <Alert variant="destructive">{insideError}</Alert>}

      {text.trim() !== "" && !ready && (
        <p className="text-sm text-muted-foreground">
          دست‌کم {toPersianDigits(searchMinLength)} حرف وارد کنید.
        </p>
      )}
      {ready && results.isPending && <p className="text-sm text-muted-foreground">در حال جستجو…</p>}
      {ready && results.isError && (
        <Alert variant="destructive">{errorMessage(results.error)}</Alert>
      )}
      {ready && results.isSuccess && results.data.items.length === 0 && (
        <div className="space-y-2">
          <p className="text-sm text-muted-foreground">عضوی با این مشخصات پیدا نشد.</p>
          <Button size="sm" onClick={() => onRegister(search)}>
            <UserPlus aria-hidden />
            ثبت این شخص
          </Button>
        </div>
      )}
      {ready && results.isSuccess && results.data.items.length > 0 && (
        <ul className="divide-y rounded-md border" aria-label="اعضای پیدا شده">
          {results.data.items.slice(0, shownMatches).map((found) => {
            const visit = found.currentVisit ?? null;

            return (
              <li key={found.id}>
                <button
                  type="button"
                  className="flex w-full items-center justify-between gap-3 px-3 py-2 text-start hover:bg-accent"
                  onClick={() => choose(found)}
                >
                  <span className="flex flex-wrap items-center gap-2">
                    {found.fullName}
                    {visit !== null && (
                      <Badge variant="secondary">داخل باشگاه — {placeOf(visit)}</Badge>
                    )}
                    {!found.isActive && <Badge variant="secondary">غیرفعال</Badge>}
                  </span>
                  <span className="text-sm text-muted-foreground" dir="ltr">
                    {formatPhone(found.phoneNumber)}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

/** "کمد ۵", or that the visit is on a reserve place. */
function placeOf(visit: NonNullable<Member["currentVisit"]>): string {
  return visit.lockerNumber === null ? "بدون کمد" : `کمد ${toPersianDigits(visit.lockerNumber)}`;
}
