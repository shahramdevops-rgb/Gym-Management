import { Pencil } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { PhoneNumber } from "@/features/members/components/MembersTable";
import { formatDate, formatDateTime } from "@/lib/format";

import type { Trainer } from "../api";

interface TrainerBoxProps {
  trainer: Trainer;
  /** While a stop or restart is being sent, so a second press sends nothing. */
  busy: boolean;
  onEdit: () => void;
  onStop: () => void;
  onRestart: () => void;
}

/**
 * One trainer as a small box (the panel's taste: a grid of boxes, not a long table): the name,
 * the phone, whether they train now and since when, their last sign-in on the site, and the
 * periods before this one with their dates (BUSINESS_RULES.md §15 *Trainers*).
 */
export function TrainerBox({ trainer, busy, onEdit, onStop, onRestart }: TrainerBoxProps) {
  const current = trainer.isActive ? trainer.periods.at(-1) : undefined;
  const past = trainer.periods.filter((period) => period.endedOn !== null);

  return (
    <article
      aria-label={trainer.fullName}
      className="flex flex-col gap-3 rounded-xl border border-s-[3px] tone-edge bg-card p-4"
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0 space-y-0.5">
          <h3 className="truncate font-medium">{trainer.fullName}</h3>
          <p className="text-sm text-muted-foreground">
            <PhoneNumber value={trainer.phoneNumber} />
          </p>
        </div>
        <Badge variant={trainer.isActive ? "success" : "secondary"}>
          {trainer.isActive ? "مربی" : "پایان‌یافته"}
        </Badge>
      </div>

      <dl className="grid grid-cols-[max-content_1fr] gap-x-3 gap-y-1 text-sm">
        {current !== undefined && (
          <>
            <dt className="text-muted-foreground">از</dt>
            <dd>{formatDate(current.startedOn)}</dd>
          </>
        )}
        <dt className="text-muted-foreground">آخرین ورود به سایت</dt>
        <dd>
          {trainer.lastSignedInAt === null
            ? "هنوز وارد سایت نشده"
            : formatDateTime(trainer.lastSignedInAt)}
        </dd>
      </dl>

      {past.length > 0 && (
        <div className="space-y-1 text-sm">
          <p className="text-muted-foreground">دوره‌های قبلی</p>
          <ul className="space-y-0.5">
            {/* Oldest first, as the API sends them; two periods can start on one day. */}
            {past.map((period, index) => (
              <li key={`${period.startedOn}-${index}`}>
                {formatDate(period.startedOn)} تا {formatDate(period.endedOn)}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mt-auto flex flex-wrap gap-2">
        <Button size="sm" variant="outline" onClick={onEdit}>
          <Pencil aria-hidden />
          ویرایش
        </Button>
        {trainer.isActive ? (
          <Button size="sm" variant="outline" disabled={busy} onClick={onStop}>
            پایان مربی‌گری
          </Button>
        ) : (
          <Button size="sm" variant="secondary" disabled={busy} onClick={onRestart}>
            شروع دوباره
          </Button>
        )}
      </div>
    </article>
  );
}
