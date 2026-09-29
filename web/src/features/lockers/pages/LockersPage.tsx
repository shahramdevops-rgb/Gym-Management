import { Search } from "lucide-react";
import { useState, type ReactNode } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useEveryoneInside, type CurrentlyInside } from "@/features/attendance/api";
import { useDeskDialog } from "@/features/attendance/components/useDeskDialog";
import { errorMessage } from "@/lib/errors";
import { gymToday, isJalaliBirthday, toPersianDigits } from "@/lib/format";
import { useDarkScreen } from "@/lib/useDarkScreen";
import { useNow } from "@/lib/useNow";
import { cn } from "@/lib/utils";

import { useAllLockers, useSetLockerOutOfService, type Locker } from "../api";
import { DeskPanel } from "../components/DeskPanel";
import { LockerCheckInDialog, type CheckInPlace } from "../components/LockerCheckInDialog";
import { LockerMap } from "../components/LockerMap";
import { LockerStats } from "../components/LockerStats";
import { LockerVisitDialog } from "../components/LockerVisitDialog";
import { ReservePlaces } from "../components/ReservePlaces";
import { lockerState } from "../lockerState";
import { stayProgress } from "../longStay";
import { nameMatches, nameSearchTerm } from "../nameSearch";

/** Which box is open. Each open gets a new id, so the next box starts fresh even for the same locker. */
type OpenBox =
  | { kind: "checkIn"; place: CheckInPlace }
  | { kind: "visit"; attendanceId: string }
  | { kind: "visitOnLocker"; lockerId: string }
  | { kind: "outOfService"; locker: Locker };

/**
 * "ورود با کمد": the desk's first screen, the same for Staff and the Owner (BUSINESS_RULES.md §6).
 * The lockers are drawn as they stand; the desk clicks the free one it gives the member and checks
 * them in from there — the only place check-in happens (§7). An occupied locker opens its visit,
 * an out-of-service one offers to bring it back.
 *
 * Two lists feed it, both refreshing every 15 seconds: the lockers (who holds each one, derived by
 * the API) and everyone inside (the visit behind each held locker or reserve place). They are
 * joined here by locker, and one page of each always holds everything — 72 lockers, and at most
 * 72 + 15 open visits.
 *
 * A third clock ticks every minute with no request at all, moving on the bar that shows how long
 * each visit has gone on (§6 *Long stay*).
 *
 * The screen is dark (§6 *The desk screen's look*), the only one that is for now, and it has a
 * name search that finds who is inside on the map (§6 *Finding a member on the map*).
 */
export function LockersPage() {
  useDarkScreen();
  const lockers = useAllLockers();
  const inside = useEveryoneInside();
  const desk = useDeskDialog();
  const now = useNow(60_000);
  const [box, setBox] = useState<{ id: number; open: OpenBox } | null>(null);
  // The locker an entry of the desk panel is pointing at, blinking on the map (§6 *The desk panel*).
  const [highlighted, setHighlighted] = useState<string | null>(null);
  const [nameQuery, setNameQuery] = useState("");

  const open = (next: OpenBox) =>
    setBox((previous) => ({ id: (previous?.id ?? 0) + 1, open: next }));
  const close = () => setBox(null);

  if (lockers.isPending || inside.isPending) {
    return (
      <PageFrame>
        <p className="text-muted-foreground">در حال بارگذاری…</p>
      </PageFrame>
    );
  }
  if (lockers.isError || inside.isError) {
    return (
      <PageFrame>
        <Alert variant="destructive">{errorMessage(lockers.error ?? inside.error)}</Alert>
      </PageFrame>
    );
  }

  const reserveVisits = inside.data.filter((visit) => visit.usesReservePlace);
  const anyLockerFree = lockers.data.some((locker) => lockerState(locker) === "free");
  const stays = new Map(
    inside.data.flatMap((visit) =>
      visit.lockerId === null ? [] : [[visit.lockerId, stayProgress(visit.checkedInAt, now)]],
    ),
  );

  // Who the name search finds: holders of occupied lockers, and members on a reserve place.
  // `null` while the field is too short to search, so the map is drawn as usual.
  const term = nameSearchTerm(nameQuery);
  const foundLockerIds =
    term === null
      ? null
      : new Set(
          lockers.data
            .filter((locker) => {
              const holder = locker.occupiedByMemberFullName;
              return holder !== null && nameMatches(holder, term);
            })
            .map((locker) => locker.id),
        );
  const foundAttendanceIds =
    term === null
      ? null
      : new Set(
          reserveVisits
            .filter((visit) => nameMatches(visit.memberFullName, term))
            .map((visit) => visit.attendanceId),
        );
  const foundCount = (foundLockerIds?.size ?? 0) + (foundAttendanceIds?.size ?? 0);

  // Whose birthday it is (the same Jalali match as the desk panel): their door or place celebrates.
  const today = gymToday();
  const birthdayVisits = inside.data.filter((visit) =>
    isJalaliBirthday(visit.memberBirthDate, today),
  );
  const birthdayLockerIds = new Set(
    birthdayVisits.flatMap((visit) => (visit.lockerId === null ? [] : [visit.lockerId])),
  );
  const birthdayAttendanceIds = new Set(birthdayVisits.map((visit) => visit.attendanceId));

  function select(locker: Locker) {
    const state = lockerState(locker);
    if (state === "free") {
      open({ kind: "checkIn", place: { kind: "locker", locker } });
    } else if (state === "occupied") {
      // By the visit when it is already known, so the box keeps it after a move to another locker.
      const visit = inside.data?.find((row) => row.lockerId === locker.id);
      open(
        visit === undefined
          ? { kind: "visitOnLocker", lockerId: locker.id }
          : { kind: "visit", attendanceId: visit.attendanceId },
      );
    } else {
      open({ kind: "outOfService", locker });
    }
  }

  // The visit is read from the latest list on every render, so هوازی or a cafe order added in its
  // box shows up there too, and a visit closed elsewhere closes its box.
  function visitFor(current: OpenBox): CurrentlyInside | undefined {
    if (current.kind === "visit") {
      return inside.data?.find((visit) => visit.attendanceId === current.attendanceId);
    }
    if (current.kind === "visitOnLocker") {
      return inside.data?.find((visit) => visit.lockerId === current.lockerId);
    }
    return undefined;
  }

  const showsAVisit =
    box !== null && (box.open.kind === "visit" || box.open.kind === "visitOnLocker");
  const openVisit = box === null ? undefined : visitFor(box.open);

  return (
    <PageFrame>
      <LockerStats
        lockers={lockers.data}
        tools={
          <NameSearch
            value={nameQuery}
            onChange={setNameQuery}
            result={term === null ? null : foundCount}
          />
        }
      />
      <LockerMap
        lockers={lockers.data}
        onSelect={select}
        highlightedLockerId={highlighted}
        stays={stays}
        foundLockerIds={foundLockerIds}
        birthdayLockerIds={birthdayLockerIds}
        aside={
          <DeskPanel
            visits={inside.data}
            onHighlight={setHighlighted}
            onOpen={(visit) => {
              setHighlighted(null);
              open({ kind: "visit", attendanceId: visit.attendanceId });
            }}
          />
        }
      />
      {/* 6.5.14's chart of today by hour goes under this row (BUSINESS_RULES.md §6). */}
      <ReservePlaces
        visits={reserveVisits}
        anyLockerFree={anyLockerFree}
        now={now}
        foundAttendanceIds={foundAttendanceIds}
        birthdayAttendanceIds={birthdayAttendanceIds}
        onOpenVisit={(visit) => open({ kind: "visit", attendanceId: visit.attendanceId })}
        onCheckIn={() => open({ kind: "checkIn", place: { kind: "reserve" } })}
      />

      {box !== null && box.open.kind === "checkIn" && (
        <LockerCheckInDialog key={box.id} place={box.open.place} onClose={close} />
      )}

      {box !== null && openVisit !== undefined && (
        <LockerVisitDialog
          key={box.id}
          visit={openVisit}
          lockers={lockers.data}
          onClose={close}
          onDeskAction={(action) => {
            close();
            desk.open(action);
          }}
        />
      )}

      {box !== null && showsAVisit && openVisit === undefined && (
        // The locker list already shows it held, but the visit list has not caught up yet (or the
        // visit was just closed elsewhere). Both refresh on their own.
        <UpdatingDialog key={box.id} onClose={close} />
      )}

      {box !== null && box.open.kind === "outOfService" && (
        <OutOfServiceDialog key={box.id} locker={box.open.locker} onClose={close} />
      )}

      {desk.dialog}
    </PageFrame>
  );
}

/** The screen's title. The clock is in the side menu, the name search on the legend's row. */
function PageFrame({ children }: { children: ReactNode }) {
  return (
    <div className="space-y-5">
      <h2 className="text-xl font-extrabold">ورود با کمد</h2>
      {children}
    </div>
  );
}

/**
 * The search field on the legend's row above the map (BUSINESS_RULES.md §6 *Finding a member on
 * the map*): a name only, among who is inside now. Escape clears it. Beside it, once there is
 * something to search for, how many were found or that nobody was; a screen reader hears it as it
 * changes. Kept to one line, so the row stays as tall as the legend.
 */
function NameSearch({
  value,
  onChange,
  result,
}: {
  value: string;
  onChange: (value: string) => void;
  /** How many the search found, or `null` while the field is too short to search. */
  result: number | null;
}) {
  return (
    <div className="flex items-center gap-3">
      <p
        role="status"
        className={cn(
          "text-xs whitespace-nowrap",
          result === 0 ? "text-warning" : "text-muted-foreground",
        )}
      >
        {result === null
          ? ""
          : result === 0
            ? "کسی با این نام داخل نیست"
            : `${toPersianDigits(result)} نفر پیدا شد`}
      </p>
      <div className="relative w-60">
        <Search
          aria-hidden
          className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
        />
        <Input
          type="search"
          aria-label="جستجوی نام در نقشه"
          placeholder="جستجوی نام"
          autoComplete="off"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Escape") {
              onChange("");
            }
          }}
          className="h-9 rounded-lg bg-background ps-9"
        />
      </div>
    </div>
  );
}

/** An out-of-service locker, clicked: the one thing to do with it is bring it back (BUSINESS_RULES.md §6). */
function OutOfServiceDialog({ locker, onClose }: { locker: Locker; onClose: () => void }) {
  const setOutOfService = useSetLockerOutOfService();
  const [error, setError] = useState<string | null>(null);

  async function bringBack() {
    setError(null);
    try {
      await setOutOfService.mutateAsync({ id: locker.id, outOfService: false });
      onClose();
    } catch (problem) {
      setError(errorMessage(problem));
    }
  }

  return (
    <Dialog open onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>کمد شماره {toPersianDigits(locker.number)}</DialogTitle>
          <DialogDescription>این کمد خارج از سرویس است.</DialogDescription>
        </DialogHeader>
        {error !== null && <Alert variant="destructive">{error}</Alert>}
        <div className="flex flex-wrap gap-2">
          <Button disabled={setOutOfService.isPending} onClick={() => void bringBack()}>
            {setOutOfService.isPending ? "در حال ثبت…" : "بازگرداندن به سرویس"}
          </Button>
          <Button variant="outline" onClick={onClose}>
            انصراف
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function UpdatingDialog({ onClose }: { onClose: () => void }) {
  return (
    <Dialog open onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>در حال به‌روزرسانی</DialogTitle>
          <DialogDescription>
            اطلاعات این کمد هنوز تازه نشده است. چند لحظه بعد دوباره امتحان کنید.
          </DialogDescription>
        </DialogHeader>
      </DialogContent>
    </Dialog>
  );
}
