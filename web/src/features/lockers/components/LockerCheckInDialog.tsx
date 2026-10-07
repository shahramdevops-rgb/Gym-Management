import { CheckCircle2, Footprints, History, Search, UserPlus, UserRound } from "lucide-react";
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
import {
  useCardioOnlyCheckIn,
  useCheckIn,
  useGuestCheckIn,
  type Attendance,
} from "@/features/attendance/api";
import type { DeskMember } from "@/features/attendance/components/CheckInOutDialog";
import { CloseButton, ConfirmButtons, LockerBox } from "@/features/attendance/components/deskParts";
import { SaleAtCheckInOffer } from "@/features/attendance/components/SaleAtCheckInOffer";
import { VisitSummary } from "@/features/attendance/components/VisitSummary";
import { cardioOnlyLabel, guestLabel } from "@/features/attendance/holder";
import { canSellPlanForToday, isMissingSubscription } from "@/features/attendance/saleAtCheckIn";
import { useCreateMember, useMemberList, type Member } from "@/features/members/api";
import { useCurrentSubscription } from "@/features/subscriptions/api";
import type { PlanChoice } from "@/features/subscriptions/components/PlanForm";
import { MemberForm } from "@/features/members/components/MemberForm";
import { searchMinLength } from "@/features/members/schemas";
import { memberDraftFromSearch } from "@/features/members/searchDraft";
import { errorMessage } from "@/lib/errors";
import { formatPhone, toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";
import { useDebouncedCallback } from "@/lib/useDebouncedCallback";

import { useSetLockerOutOfService, type Locker } from "../api";
import { GuestNameForm } from "./GuestNameForm";
import { LockerTodayHistory } from "./LockerTodayHistory";

/** Long enough to skip the keys of one word, short enough to feel immediate (the same as the search screen). */
const searchDelayMs = 300;

/** How many matches the box lists; a longer list belongs on the search screen. */
const shownMatches = 8;

/** Where the visit goes: the locker the desk clicked, or a reserve place (BUSINESS_RULES.md §6). */
export type CheckInPlace = { kind: "locker"; locker: Locker } | { kind: "reserve" };

/**
 * What was sold with the check-in, if anything, or that it was a cardio-only visit, which sells
 * nothing and consumes no session (BUSINESS_RULES.md §7 *Cardio-only visit*); it names the result.
 */
type Sold = "nothing" | "singleVisit" | "plan" | "cardioOnly";

type NeedsSubscription = {
  kind: "needsSubscription";
  member: DeskMember;
  heading: string;
  reason: string;
  /** Whether a plan sold now would start today, so it can be sold here (roadmap 6.5.7). */
  planHere: boolean;
};

type Step =
  | { kind: "search" }
  | { kind: "register"; text: string }
  /** «ورود مهمان»: a name and nothing else (BUSINESS_RULES.md §7 *Guest visit*). */
  | { kind: "guest" }
  | { kind: "guestCheckedIn"; attendance: Attendance }
  | { kind: "confirm"; member: DeskMember }
  /**
   * «ورود فقط هوازی», asked again before anything is sent, like an ordinary check-in. «انصراف»
   * goes back to where it was chosen: the confirm step, or the offer after a refusal.
   */
  | { kind: "confirmCardioOnly"; member: DeskMember; back: "confirm" | NeedsSubscription }
  | { kind: "checkedIn"; member: DeskMember; attendance: Attendance; sold: Sold }
  | NeedsSubscription
  | { kind: "failed"; reason: string }
  /** Who had the locker today (BUSINESS_RULES.md §6); «بازگشت» goes back to the search. */
  | { kind: "history"; locker: Locker };

const checkedInTitle: Record<Sold, string> = {
  nothing: "ورود ثبت شد",
  singleVisit: "ورود تک‌جلسه‌ای ثبت شد",
  plan: "اشتراک فروخته شد و ورود ثبت شد",
  cardioOnly: "ورود فقط هوازی ثبت شد",
};

interface LockerCheckInDialogProps {
  place: CheckInPlace;
  onClose: () => void;
}

/**
 * The whole check-in, from the locker the desk clicked (BUSINESS_RULES.md §7 *Confirming at the
 * front desk*): find the member by name or mobile, confirm, and the visit is recorded with that
 * locker. Everything the search screen used to offer for check-in lives here now: registering a
 * person who is not found, and selling a single visit or a plan when the member has nothing usable
 * (§4) — checked in with this same locker, in the same request, so the desk never goes back to the
 * map to choose the locker a second time (roadmap 6.5.7).
 *
 * A person just registered has no subscription by definition, so the box goes straight to the
 * sale instead of asking to confirm a check-in that could only be refused.
 *
 * A member who is already inside is marked in the results, and choosing them shows why not and
 * sends nothing; the API would refuse it anyway (`Attendance.AlreadyCheckedIn`).
 *
 * A guest the gym lets in for free (BUSINESS_RULES.md §7 *Guest visit*) is the third way in, beside
 * finding and registering a member: «ورود مهمان» asks for their full name and nothing else.
 *
 * Once a member is chosen, «ورود فقط هوازی» lets them in without a session (BUSINESS_RULES.md §7
 * *Cardio-only visit*). No plan is needed for it (roadmap 6.5.35), so it is offered again under the
 * sale when an ordinary check-in is refused or a member was just registered: someone with no plan,
 * or one that ran out, who only wants the treadmill comes in without buying anything.
 *
 * For a locker (not a reserve place), the box also shows who had it today, before a member is
 * chosen (BUSINESS_RULES.md §6 *Who had a locker today*).
 *
 * Like the check-out box, it closes only with its ✕ or its own buttons: the outcome is what the
 * desk must read.
 */
export function LockerCheckInDialog({ place, onClose }: LockerCheckInDialogProps) {
  const [step, setStep] = useState<Step>({ kind: "search" });
  const lockerId = place.kind === "locker" ? place.locker.id : null;

  const checkIn = useCheckIn();
  const guestCheckIn = useGuestCheckIn();
  const cardioOnlyCheckIn = useCardioOnlyCheckIn();
  const createMember = useCreateMember();
  const setOutOfService = useSetLockerOutOfService();
  const [outOfServiceError, setOutOfServiceError] = useState<string | null>(null);
  const busy =
    checkIn.isPending ||
    guestCheckIn.isPending ||
    cardioOnlyCheckIn.isPending ||
    setOutOfService.isPending;
  // Which request is in flight: the single-visit button shows its own "در حال ثبت…".
  const sellingSingleVisit = checkIn.isPending && checkIn.variables?.sale?.kind === "SingleVisit";

  const title =
    place.kind === "locker" ? `کمد شماره ${toPersianDigits(place.locker.number)}` : "ورود بدون کمد";

  async function confirmCheckIn(member: DeskMember) {
    try {
      const attendance = await checkIn.mutateAsync({ memberId: member.id, lockerId });
      setStep({ kind: "checkedIn", member, attendance, sold: "nothing" });
    } catch (problem) {
      const reason = errorMessage(problem);
      // The walk-in case the desk meets all day: offer the way in rather than just saying no.
      setStep(
        isMissingSubscription(problem)
          ? {
              kind: "needsSubscription",
              member,
              heading: "ورود ممکن نیست",
              reason: `${member.fullName}: ${reason}`,
              planHere: canSellPlanForToday(problem),
            }
          : { kind: "failed", reason },
      );
    }
  }

  async function confirmCardioOnly(member: DeskMember) {
    try {
      const attendance = await cardioOnlyCheckIn.mutateAsync({ memberId: member.id, lockerId });
      setStep({ kind: "checkedIn", member, attendance, sold: "cardioOnly" });
    } catch (problem) {
      setStep({ kind: "failed", reason: `${member.fullName}: ${errorMessage(problem)}` });
    }
  }

  async function sellSingleVisit(member: DeskMember) {
    try {
      const attendance = await checkIn.mutateAsync({
        memberId: member.id,
        lockerId,
        sale: { kind: "SingleVisit" },
      });
      setStep({ kind: "checkedIn", member, attendance, sold: "singleVisit" });
    } catch (problem) {
      setStep({ kind: "failed", reason: errorMessage(problem) });
    }
  }

  /** A rejection is left to the plan form, which shows it by the field or above its button. */
  async function sellPlan(member: DeskMember, plan: PlanChoice) {
    const attendance = await checkIn.mutateAsync({
      memberId: member.id,
      lockerId,
      sale: { kind: "Membership", ...plan },
    });
    setStep({ kind: "checkedIn", member, attendance, sold: "plan" });
  }

  /** A rejection is left to the name form, which shows it under the field or above its button. */
  async function letGuestIn(guestName: string) {
    const attendance = await guestCheckIn.mutateAsync({ guestName, lockerId });
    setStep({ kind: "guestCheckedIn", attendance });
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
            <div className="flex">
              <Button size="sm" variant="outline" onClick={() => setStep({ kind: "guest" })}>
                <UserRound aria-hidden />
                ورود مهمان
              </Button>
            </div>
            {place.kind === "locker" && (
              <div className="space-y-2 border-t pt-3">
                {outOfServiceError !== null && (
                  <Alert variant="destructive">{outOfServiceError}</Alert>
                )}
                <div className="flex flex-wrap gap-2">
                  <TodayHistoryButton
                    onClick={() => setStep({ kind: "history", locker: place.locker })}
                  />
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={setOutOfService.isPending}
                    onClick={() => void takeOutOfService(place.locker)}
                  >
                    خارج از سرویس کردن این کمد
                  </Button>
                </div>
              </div>
            )}
          </>
        )}

        {step.kind === "register" && (
          // Registering here rather than on another screen: the person is standing at the desk, and
          // the next step is letting them in (roadmap 6.5.4). They have no subscription by
          // definition, so the box goes straight to selling them one (roadmap 6.5.7).
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
                setStep({
                  kind: "needsSubscription",
                  member,
                  heading: "عضو جدید ثبت شد",
                  reason: `${member.fullName} اشتراکی ندارد. برای ورود، تک‌جلسه یا اشتراک بفروشید.`,
                  planHere: true,
                });
              }}
              actions={
                <Button type="button" variant="ghost" onClick={() => setStep({ kind: "search" })}>
                  بازگشت
                </Button>
              }
            />
          </>
        )}

        {step.kind === "guest" && (
          <>
            <DialogHeader>
              <DialogTitle>{title} — ورود مهمان</DialogTitle>
              <DialogDescription>
                برای کسی که باشگاه رایگان راه می‌دهد: فقط نام او ثبت می‌شود؛ جلسه‌ای کم نمی‌شود و
                چیزی فروخته نمی‌شود.
              </DialogDescription>
            </DialogHeader>
            <GuestNameForm onSubmit={letGuestIn} onBack={() => setStep({ kind: "search" })} />
          </>
        )}

        {step.kind === "guestCheckedIn" && (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-success">
                <CheckCircle2 className="size-5" aria-hidden />
                ورود مهمان ثبت شد
              </DialogTitle>
              <DialogDescription>
                {step.attendance.guestName} · {guestLabel}
              </DialogDescription>
            </DialogHeader>
            <LockerBox number={step.attendance.lockerNumber} />
            <CloseButton onClose={onClose} />
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
            <FrozenPlanWarning memberId={step.member.id} />
            <ConfirmButtons
              label="بله، ورود ثبت شود"
              pending={checkIn.isPending}
              onConfirm={() => void confirmCheckIn(step.member)}
              onCancel={() => setStep({ kind: "search" })}
            />
            <div className="border-t pt-3">
              <CardioOnlyButton
                disabled={checkIn.isPending}
                onClick={() =>
                  setStep({ kind: "confirmCardioOnly", member: step.member, back: "confirm" })
                }
              />
            </div>
          </>
        )}

        {step.kind === "confirmCardioOnly" && (
          <>
            <DialogHeader>
              <DialogTitle>ورود فقط هوازی</DialogTitle>
              <DialogDescription>
                آیا از ثبت ورود فقط هوازی{" "}
                <strong className="text-foreground">{step.member.fullName}</strong>{" "}
                {place.kind === "locker"
                  ? `با کمد شماره ${toPersianDigits(place.locker.number)}`
                  : "بدون کمد"}{" "}
                مطمئن هستید؟ جلسه‌ای کم نمی‌شود و اشتراک لازم نیست. خروج فقط بعد از ثبت مبلغ هوازی
                ممکن است.
              </DialogDescription>
            </DialogHeader>
            <ConfirmButtons
              label="بله، ورود فقط هوازی ثبت شود"
              pending={cardioOnlyCheckIn.isPending}
              onConfirm={() => void confirmCardioOnly(step.member)}
              onCancel={() =>
                setStep(
                  step.back === "confirm" ? { kind: "confirm", member: step.member } : step.back,
                )
              }
            />
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
            <LockerTodayHistory lockerId={step.locker.id} />
            <div className="flex">
              <Button variant="outline" onClick={() => setStep({ kind: "search" })}>
                بازگشت
              </Button>
            </div>
          </>
        )}

        {step.kind === "checkedIn" && (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-success">
                <CheckCircle2 className="size-5" aria-hidden />
                {checkedInTitle[step.sold]}
              </DialogTitle>
              <DialogDescription>
                {step.member.fullName}
                {step.sold === "cardioOnly" && ` · ${cardioOnlyLabel}`}
              </DialogDescription>
            </DialogHeader>
            <LockerBox number={step.attendance.lockerNumber} />
            {step.attendance.unfrozenDays !== undefined &&
              step.attendance.unfrozenDays !== null && (
                <Alert role="status">{unfrozenNotice(Number(step.attendance.unfrozenDays))}</Alert>
              )}
            <VisitSummary memberId={step.member.id} />
            <CloseButton onClose={onClose} />
          </>
        )}

        {step.kind === "needsSubscription" && (
          <>
            <DialogHeader>
              <DialogTitle>{step.heading}</DialogTitle>
              <DialogDescription>{step.reason}</DialogDescription>
            </DialogHeader>
            <SaleAtCheckInOffer
              memberId={step.member.id}
              planHere={step.planHere}
              selling={sellingSingleVisit}
              onSellSingleVisit={() => void sellSingleVisit(step.member)}
              onSellPlan={(plan) => sellPlan(step.member, plan)}
            />
            <div className="border-t pt-3">
              <CardioOnlyButton
                disabled={checkIn.isPending}
                onClick={() =>
                  setStep({ kind: "confirmCardioOnly", member: step.member, back: step })
                }
              />
            </div>
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
 * Said before the desk confirms: checking in a member whose plan is frozen ends the freeze
 * (BUSINESS_RULES.md §4 *Freeze*, roadmap 6.5.9). Read from the plan the profile's card shows, which
 * already prefers a plan usable today over a frozen one, the same as the API: a member holding a
 * single visit for today is not warned, because their freeze is left alone. Only a warning; the
 * API decides, and the box says afterwards what it did.
 */
function FrozenPlanWarning({ memberId }: { memberId: string }) {
  const subscription = useCurrentSubscription(memberId);

  if (subscription.data?.status !== "Frozen") {
    return null;
  }

  return <Alert>اشتراک او فریز است. با ثبت این ورود، اشتراک از حالت فریز خارج می‌شود.</Alert>;
}

/**
 * Opens who had the locker today (BUSINESS_RULES.md §6 *Who had a locker today*). Offered before a
 * member is chosen, so the desk never has to pick someone just to ask.
 */
function TodayHistoryButton({ onClick }: { onClick: () => void }) {
  return (
    <Button size="sm" variant="ghost" onClick={onClick}>
      <History aria-hidden />
      تاریخچه امروز این کمد
    </Button>
  );
}

/**
 * «ورود فقط هوازی» (BUSINESS_RULES.md §7 *Cardio-only visit*), in its own colour, both beside the
 * ordinary check-in and under the sale offered after a refusal.
 */
function CardioOnlyButton({ disabled, onClick }: { disabled: boolean; onClick: () => void }) {
  return (
    <Button variant="outline" className="border-cardio" disabled={disabled} onClick={onClick}>
      <Footprints aria-hidden />
      ورود فقط هوازی
    </Button>
  );
}

/** What the desk is told after a check-in that ended a freeze. */
function unfrozenNotice(days: number): string {
  const unfrozen = "اشتراک فریز بود و با این ورود از حالت فریز خارج شد";

  // Frozen and unfrozen on the same day adds nothing to the end (§4), so there is nothing to count.
  return days === 0
    ? `${unfrozen}.`
    : `${unfrozen}؛ ${toPersianDigits(days)} روز به پایان آن اضافه شد.`;
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
