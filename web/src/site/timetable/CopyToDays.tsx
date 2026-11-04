import { Copy } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";

import type { DayOfWeek } from "./api";
import { dayNames, weekDays } from "./labels";

/**
 * «کپی به روزهای دیگر» under a day of the weekly pattern (roadmap 15.6a): the trainer picks the
 * other days and the day's sessions take the place of theirs. Only the editor's drafts change;
 * nothing is sent until «ذخیره الگو», so the usual check and warning still come first.
 */
export function CopyToDays({
  from,
  onCopy,
  disabled,
}: {
  from: DayOfWeek;
  onCopy: (days: DayOfWeek[]) => void;
  disabled: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [chosen, setChosen] = useState<DayOfWeek[]>([]);
  const others = weekDays.filter((day) => day !== from);

  function close() {
    setOpen(false);
    setChosen([]);
  }

  function toggle(day: DayOfWeek) {
    setChosen(chosen.includes(day) ? chosen.filter((other) => other !== day) : [...chosen, day]);
  }

  if (!open) {
    return (
      <Button
        variant="outline"
        size="sm"
        disabled={disabled}
        aria-label={`کپی ${dayNames[from]} به روزهای دیگر`}
        onClick={() => setOpen(true)}
      >
        <Copy aria-hidden />
        کپی به روزهای دیگر
      </Button>
    );
  }

  return (
    <div
      role="group"
      aria-label={`کپی ${dayNames[from]} به روزهای دیگر`}
      className="space-y-2 rounded-lg border p-2"
    >
      <p className="text-sm">سانس‌های {dayNames[from]} به این روزها کپی شود:</p>
      <div className="flex flex-wrap gap-1.5">
        {others.map((day) => {
          const on = chosen.includes(day);
          return (
            <Button
              key={day}
              size="sm"
              variant={on ? "default" : "outline"}
              aria-pressed={on}
              disabled={disabled}
              onClick={() => toggle(day)}
            >
              {dayNames[day]}
            </Button>
          );
        })}
      </div>
      <p className="text-xs text-muted-foreground">سانس‌های فعلی این روزها جایگزین می‌شوند.</p>
      <div className="flex gap-2">
        <Button
          size="sm"
          disabled={disabled || chosen.length === 0}
          onClick={() => {
            onCopy(chosen);
            close();
          }}
        >
          کپی
        </Button>
        <Button size="sm" variant="outline" disabled={disabled} onClick={close}>
          انصراف
        </Button>
      </div>
    </div>
  );
}
