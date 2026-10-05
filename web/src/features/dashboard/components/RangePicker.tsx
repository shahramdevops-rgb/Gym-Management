import { useState } from "react";

import { JalaliCalendarField } from "@/components/FormField";
import { cn } from "@/lib/utils";

import { presetOf, presetRange, rangePresetLabels, rangePresets, type ReportRange } from "../range";

/** A range being chosen by hand can have a side missing for a moment. */
export type RangeDraft = Partial<ReportRange>;

interface RangePickerProps {
  range: RangeDraft;
  today: string;
  onChange: (range: RangeDraft) => void;
  /** Why the API would refuse the range, as its Persian message; shown under «تا تاریخ». */
  error?: string;
}

/**
 * A preset as a pill on the dashboard's banner: the chosen one filled white, the others see-through,
 * so the banner's colour shows behind them.
 */
function PresetPill({
  pressed,
  onClick,
  children,
}: {
  pressed: boolean;
  onClick: () => void;
  children: string;
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onClick}
      className={cn(
        "h-8 rounded-full px-4 text-sm font-medium whitespace-nowrap transition outline-none focus-visible:ring-[3px] focus-visible:ring-white/60",
        pressed
          ? "bg-white text-(--hero-via) shadow-md"
          : "bg-white/15 text-white ring-1 ring-white/25 hover:bg-white/25",
      )}
    >
      {children}
    </button>
  );
}

/**
 * The dashboard's range (§12 *Dashboard*): a preset in one press, or «دلخواه» for two Jalali date
 * boxes. A range of one's own that matches a preset lights that preset's button, so the boxes stay
 * closed until they are asked for.
 *
 * It sits on the dashboard's banner: the presets are pills on its colour, and the date boxes and the
 * error open in a card-coloured panel under them, where the fields and the red read as they do
 * everywhere else.
 */
export function RangePicker({ range, today, onChange, error }: RangePickerProps) {
  const complete = range.from !== undefined && range.to !== undefined;
  const preset = complete ? presetOf(range as ReportRange, today) : undefined;
  const [customOpen, setCustomOpen] = useState(false);
  const showFields = customOpen || preset === undefined;

  return (
    <div className="space-y-3">
      <div role="group" aria-label="بازهٔ گزارش" className="flex flex-wrap gap-2">
        {rangePresets.map((item) => (
          <PresetPill
            key={item}
            pressed={!customOpen && preset === item}
            onClick={() => {
              setCustomOpen(false);
              onChange(presetRange(item, today));
            }}
          >
            {rangePresetLabels[item]}
          </PresetPill>
        ))}
        <PresetPill pressed={showFields} onClick={() => setCustomOpen(true)}>
          دلخواه
        </PresetPill>
      </div>

      {(showFields || error !== undefined) && (
        <div className="rounded-xl bg-card p-3 text-card-foreground shadow-lg">
          {showFields ? (
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <JalaliCalendarField
                label="از تاریخ"
                value={range.from ?? ""}
                onChange={(iso) => onChange({ ...range, from: iso === "" ? undefined : iso })}
              />
              <JalaliCalendarField
                label="تا تاریخ"
                value={range.to ?? ""}
                error={error}
                onChange={(iso) => onChange({ ...range, to: iso === "" ? undefined : iso })}
              />
            </div>
          ) : (
            <p className="text-sm text-destructive">{error}</p>
          )}
        </div>
      )}
    </div>
  );
}
