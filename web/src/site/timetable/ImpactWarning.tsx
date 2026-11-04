import { Button } from "@/components/ui/button";
import { toPersianDigits } from "@/lib/format";

import type { TimetableImpact } from "./api";
import { formatDayLabel, formatSessionTimes } from "./labels";

/** A change checked and held until the trainer answers the warning. */
export interface HeldChange {
  impact: TimetableImpact;
  save: () => void;
}

/** The sessions named one by one; a pattern's change may touch many more dates than fit a phone. */
const shownSessions = 8;

/**
 * The warning before a change of the timetable that touches students (BUSINESS_RULES.md §15
 * *Students in sessions*, roadmap 15.6b): how many students, and each session it removes, moves
 * or leaves with too little room. The trainer saves anyway or not; it stands where the change's
 * own buttons were.
 */
export function ImpactWarning({
  impact,
  pending,
  onSave,
  onCancel,
}: {
  impact: TimetableImpact;
  pending: boolean;
  onSave: () => void;
  onCancel: () => void;
}) {
  const hidden = impact.sessions.length - shownSessions;

  return (
    <div
      role="alert"
      className="space-y-3 rounded-xl border border-destructive/30 bg-destructive/8 p-3 text-sm"
    >
      <p className="font-semibold text-destructive">
        این تغییر به {toPersianDigits(impact.students)} شاگرد می‌رسد:
      </p>
      <ul aria-label="سانس‌های درگیر" className="space-y-1">
        {impact.sessions.slice(0, shownSessions).map((session) => (
          <li key={session.date + session.start + session.end}>
            {formatDayLabel(session.date)}، {formatSessionTimes(session)}:{" "}
            {toPersianDigits(session.students)} شاگرد،{" "}
            {session.removed ? "سانس لغو می‌شود" : "ظرفیت از شاگردانش کمتر می‌شود"}
          </li>
        ))}
        {hidden > 0 && <li>و {toPersianDigits(hidden)} سانس دیگر</li>}
      </ul>
      <p className="text-muted-foreground">شاگردان سانسی که لغو شود، آن را «لغو شد» می‌بینند.</p>
      <div className="flex flex-wrap gap-2">
        <Button variant="destructive" disabled={pending} onClick={onSave}>
          {pending ? "در حال ثبت…" : "ذخیره با این حال"}
        </Button>
        <Button variant="outline" disabled={pending} onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </div>
  );
}
