import { Plus, Trash2 } from "lucide-react";
import { useId } from "react";

import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { SessionInput } from "./api";
import { toDraft, type DraftSession } from "./drafts";
import { nextSession } from "./labels";

const hours = Array.from({ length: 24 }, (_, hour) => String(hour).padStart(2, "0"));
// On 5-minute steps (BUSINESS_RULES.md §15 *The trainer's sessions*).
const minutes = Array.from({ length: 12 }, (_, step) => String(step * 5).padStart(2, "0"));
const capacities = Array.from({ length: 50 }, (_, index) => index + 1);
const maxSessions = 20;

// The site keeps its own copy of the panel's select look (FormField's), as it does its labels.
const selectClassName = cn(
  "select-box h-11 min-w-0 rounded-lg border border-input bg-card px-1.5 py-1 text-base shadow-xs outline-none",
  "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/30",
  "[&_option]:bg-card [&_option]:text-card-foreground",
);

/**
 * One day's sessions, each a small box: start, end, capacity, «VIP» and «حذف». The times are an hour and
 * a minute on 5-minute steps, so the boxes only make times the API accepts; a box with a problem
 * (`sessionProblems`) turns red and says it under its own times, and a day stops at 20 sessions.
 */
export function SessionsEditor({
  label,
  sessions,
  onChange,
  problems,
  disabled = false,
}: {
  /** Who the sessions belong to, for the buttons read aloud: «شنبه». */
  label: string;
  sessions: DraftSession[];
  onChange: (sessions: DraftSession[]) => void;
  /** Each session's problem by its key, once the trainer has tried to save. */
  problems?: ReadonlyMap<string, string>;
  disabled?: boolean;
}) {
  function change(key: string, part: Partial<SessionInput>) {
    onChange(sessions.map((session) => (session.key === key ? { ...session, ...part } : session)));
  }

  return (
    <div className="space-y-2">
      {sessions.length === 0 && <p className="text-sm text-muted-foreground">بدون سانس</p>}
      <ul aria-label={`سانس‌های ${label}`} className="space-y-2">
        {sessions.map((session, index) => {
          const problem = problems?.get(session.key);
          return (
            <li
              key={session.key}
              aria-label={`سانس ${toPersianDigits(index + 1)}`}
              data-session-problem={problem === undefined ? undefined : ""}
              className={cn(
                "flex flex-wrap items-end gap-x-2 gap-y-2 rounded-lg border bg-muted/30 p-2",
                problem !== undefined && "border-destructive bg-destructive/8",
              )}
            >
              <TimeField
                label="شروع"
                value={session.start}
                disabled={disabled}
                onChange={(start) => change(session.key, { start })}
              />
              <TimeField
                label="پایان"
                value={session.end}
                disabled={disabled}
                onChange={(end) => change(session.key, { end })}
              />
              <CapacityField
                value={session.capacity}
                disabled={disabled}
                onChange={(capacity) => change(session.key, { capacity })}
              />
              {/* A toggle, as the days of «کپی به روزهای دیگر» are (BUSINESS_RULES.md §15 *VIP sessions*). */}
              <Button
                variant={session.isVip ? "default" : "outline"}
                className="h-11"
                aria-pressed={session.isVip}
                aria-label={`VIP سانس ${toPersianDigits(index + 1)}`}
                disabled={disabled}
                onClick={() => change(session.key, { isVip: !session.isVip })}
              >
                VIP
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="ms-auto"
                aria-label={`حذف سانس ${toPersianDigits(index + 1)}`}
                disabled={disabled}
                onClick={() => onChange(sessions.filter((other) => other.key !== session.key))}
              >
                <Trash2 aria-hidden />
              </Button>
              {problem !== undefined && (
                <p className="w-full text-sm text-destructive">{problem}</p>
              )}
            </li>
          );
        })}
      </ul>
      {sessions.length >= maxSessions && (
        <p className="text-xs text-muted-foreground">هر روز حداکثر ۲۰ سانس می‌تواند داشته باشد.</p>
      )}
      <Button
        variant="outline"
        size="sm"
        disabled={disabled || sessions.length >= maxSessions}
        aria-label={`افزودن سانس به ${label}`}
        onClick={() => onChange([...sessions, toDraft(nextSession(sessions))])}
      >
        <Plus aria-hidden />
        افزودن سانس
      </Button>
    </div>
  );
}

/** `HH:mm` as an hour box and a minute box. */
function TimeField({
  label,
  value,
  onChange,
  disabled,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
}) {
  const labelId = useId();
  const [hour = "00", minute = "00"] = value.split(":");

  return (
    <div className="space-y-1">
      <Label id={labelId} className="text-xs text-muted-foreground">
        {label}
      </Label>
      {/* An hour reads left to right whatever the page's direction: ۱۳:۳۰, not ۳۰:۱۳. */}
      <div role="group" aria-labelledby={labelId} dir="ltr" className="flex items-center gap-0.5">
        <select
          aria-label={`ساعت ${label}`}
          className={cn(selectClassName, "w-14")}
          value={hour}
          disabled={disabled}
          onChange={(event) => onChange(`${event.target.value}:${minute}`)}
        >
          {hours.map((option) => (
            <option key={option} value={option}>
              {toPersianDigits(option)}
            </option>
          ))}
        </select>
        <span aria-hidden>:</span>
        <select
          aria-label={`دقیقه ${label}`}
          className={cn(selectClassName, "w-14")}
          value={minute}
          disabled={disabled}
          onChange={(event) => onChange(`${hour}:${event.target.value}`)}
        >
          {minutes.map((option) => (
            <option key={option} value={option}>
              {toPersianDigits(option)}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}

function CapacityField({
  value,
  onChange,
  disabled,
}: {
  value: number;
  onChange: (value: number) => void;
  disabled: boolean;
}) {
  const id = useId();

  return (
    <div className="space-y-1">
      <Label htmlFor={id} className="text-xs text-muted-foreground">
        ظرفیت
      </Label>
      <select
        id={id}
        className={cn(selectClassName, "w-16")}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(Number(event.target.value))}
      >
        {capacities.map((option) => (
          <option key={option} value={option}>
            {toPersianDigits(option)}
          </option>
        ))}
      </select>
    </div>
  );
}
