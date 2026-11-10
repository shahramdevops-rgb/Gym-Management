import { useId, useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { formatDate, toPersianDigits } from "@/lib/format";
import { normalizeDigits } from "@/lib/normalize";

import { periodEnd, periodMaxDays } from "./period";

/**
 * The box where the trainer types a coaching period's days (BUSINESS_RULES.md §15 *The coaching
 * period*, roadmap 15.7h): on «پذیرفتن», on «تمدید», and for the days left of a student from before
 * it. Persian and English digits alike; the dates it gives are shown under it as they are typed, so
 * the trainer sees where the period ends before saving.
 */
export function PeriodDaysForm({
  label,
  startsOn,
  defaultDays,
  minDays,
  submitLabel,
  pending,
  note,
  onSubmit,
  onCancel,
}: {
  label: string;
  /** The period's first day, for the dates under the box. */
  startsOn: string;
  defaultDays: number | null;
  minDays: number;
  submitLabel: string;
  pending: boolean;
  /** A warning for the days typed, e.g. that the fixed schedule is cut; `null` for none. */
  note?: (days: number) => string | null;
  onSubmit: (days: number) => void;
  /** «انصراف»; none when the box is the card's only way forward. */
  onCancel?: () => void;
}) {
  const id = useId();
  const [typed, setTyped] = useState(defaultDays === null ? "" : String(defaultDays));
  const days = typed === "" ? null : Number(typed);
  const valid = days !== null && days >= minDays && days <= periodMaxDays;
  const warning = valid && note !== undefined ? note(days) : null;

  return (
    <form
      className="space-y-2"
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        if (valid) {
          onSubmit(days);
        }
      }}
    >
      <div className="flex items-center gap-2">
        <Label htmlFor={id}>{label}</Label>
        <Input
          id={id}
          inputMode="numeric"
          autoFocus
          className="w-16 text-center"
          value={toPersianDigits(typed)}
          disabled={pending}
          onChange={(event) =>
            setTyped(normalizeDigits(event.target.value).replace(/\D/g, "").slice(0, 2))
          }
        />
        <span className="text-sm">روز</span>
      </div>
      <p className="text-xs text-muted-foreground">
        {valid
          ? `از ${formatDate(startsOn)} تا ${formatDate(periodEnd(startsOn, days))}`
          : `بین ${toPersianDigits(minDays)} تا ${toPersianDigits(periodMaxDays)} روز`}
      </p>
      {warning !== null && <Alert>{warning}</Alert>}
      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={!valid || pending}>
          {pending ? "در حال ثبت…" : submitLabel}
        </Button>
        {onCancel !== undefined && (
          <Button type="button" variant="outline" size="sm" disabled={pending} onClick={onCancel}>
            انصراف
          </Button>
        )}
      </div>
    </form>
  );
}
