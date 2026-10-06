import { useState } from "react";

import { JalaliCalendarField } from "@/components/FormField";
import { Button } from "@/components/ui/button";

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
 * The dashboard's range (§12 *Dashboard*): a preset in one press, or «دلخواه» for two Jalali date
 * boxes. A range of one's own that matches a preset lights that preset's button, so the boxes stay
 * closed until they are asked for.
 */
export function RangePicker({ range, today, onChange, error }: RangePickerProps) {
  const complete = range.from !== undefined && range.to !== undefined;
  const preset = complete ? presetOf(range as ReportRange, today) : undefined;
  const [customOpen, setCustomOpen] = useState(false);
  const showFields = customOpen || preset === undefined;

  return (
    <div className="space-y-3">
      <div role="group" aria-label="بازهٔ گزارش" className="flex flex-wrap gap-1">
        {rangePresets.map((item) => {
          const pressed = !customOpen && preset === item;
          return (
            <Button
              key={item}
              type="button"
              size="sm"
              aria-pressed={pressed}
              variant={pressed ? "secondary" : "outline"}
              onClick={() => {
                setCustomOpen(false);
                onChange(presetRange(item, today));
              }}
            >
              {rangePresetLabels[item]}
            </Button>
          );
        })}
        <Button
          type="button"
          size="sm"
          aria-pressed={showFields}
          variant={showFields ? "secondary" : "outline"}
          onClick={() => setCustomOpen(true)}
        >
          دلخواه
        </Button>
      </div>

      {showFields && (
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
      )}
      {!showFields && error !== undefined && <p className="text-sm text-destructive">{error}</p>}
    </div>
  );
}
