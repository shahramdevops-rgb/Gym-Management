import { ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import type { CurrentlyInside } from "@/features/attendance/api";
import { toPersianDigits } from "@/lib/format";

/** How many visits can be inside with no locker at once: `Attendance.ReservePlaceCount` (BUSINESS_RULES.md §6). */
export const reservePlaceCount = 15;

interface ReservePlacesProps {
  /** The open visits on a reserve place. */
  visits: CurrentlyInside[];
  /** Whether a locker is both in service and free, which rules a reserve place out. */
  anyLockerFree: boolean;
  onOpenVisit: (visit: CurrentlyInside) => void;
  onCheckIn: () => void;
}

/**
 * The 15 reserve places (BUSINESS_RULES.md §6 *Reserve places*), tucked behind one small control
 * that says how many are used. Every locker being full is rare, and 15 boxes that are nearly always
 * empty must not take the desk's room.
 *
 * A used place shows the member's name where a locker would show its number and opens the same box
 * as a locker. An empty one checks someone in only when no locker is free; until then it says why
 * not, rather than offering a check-in the API would refuse (`Attendance.LockersStillFree`).
 */
export function ReservePlaces({
  visits,
  anyLockerFree,
  onOpenVisit,
  onCheckIn,
}: ReservePlacesProps) {
  const [open, setOpen] = useState(false);
  const empty = Math.max(0, reservePlaceCount - visits.length);

  return (
    <div className="space-y-3">
      <Button variant="outline" size="sm" aria-expanded={open} onClick={() => setOpen(!open)}>
        ورود بدون کمد {toPersianDigits(visits.length)} از {toPersianDigits(reservePlaceCount)}
        {open ? <ChevronUp aria-hidden /> : <ChevronDown aria-hidden />}
      </Button>

      {open && (
        <div className="space-y-2 rounded-lg border p-3">
          {anyLockerFree && (
            <p className="text-sm text-muted-foreground">
              تا وقتی کمد آزادی هست، ورود بدون کمد ممکن نیست؛ کمد آزاد را از نقشه انتخاب کنید.
            </p>
          )}
          <ul aria-label="جاهای ورود بدون کمد" className="grid grid-cols-5 gap-2">
            {visits.map((visit) => (
              <li key={visit.attendanceId}>
                <button
                  type="button"
                  className="flex h-14 w-full items-center justify-center rounded-sm border-2 border-destructive bg-destructive/15 px-1 text-center text-xs font-medium text-destructive hover:bg-destructive/25"
                  onClick={() => onOpenVisit(visit)}
                >
                  {visit.memberFullName}
                </button>
              </li>
            ))}
            {Array.from({ length: empty }, (_, index) => (
              <li key={`empty-${index}`}>
                <button
                  type="button"
                  aria-label="جای خالی ورود بدون کمد"
                  disabled={anyLockerFree}
                  className="flex h-14 w-full items-center justify-center rounded-sm border-2 border-dashed text-xs text-muted-foreground hover:bg-accent disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={onCheckIn}
                >
                  خالی
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
