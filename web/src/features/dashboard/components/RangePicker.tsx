import { useState } from "react";

import { JalaliCalendarField } from "@/components/FormField";
import { Button } from "@/components/ui/button";
import { choiceClass, filterEdge } from "@/lib/edges";
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
 * The dashboard's range (§12 *Dashboard*): a preset in one press, or «دلخواه» for two Jalali date
 * boxes. A range of one's own that matches a preset lights that preset's button, so the boxes stay
 * closed until they are asked for.
 *
 * Drawn like every page's filters (13.2i, asked by the developer: the dashboard "exactly like the
 * other pages"): one row that wraps, under the banner, the presets as a filter's choices and the
 * date boxes on the page's colour, both edged in the tone with the thick edge at their end.
 */
export function RangePicker({ range, today, onChange, error }: RangePickerProps) {
  const complete = range.from !== undefined && range.to !== undefined;
  const preset = complete ? presetOf(range as ReportRange, today) : undefined;
  const [customOpen, setCustomOpen] = useState(false);
  const showFields = customOpen || preset === undefined;

  return (
    <div className="flex flex-wrap items-end gap-3">
      <div role="group" aria-label="بازهٔ گزارش" className="flex flex-wrap gap-1">
        {rangePresets.map((item) => (
          <Button
            key={item}
            type="button"
            size="sm"
            variant="ghost"
            className={cn("h-9", choiceClass(!customOpen && preset === item, "end"))}
            aria-pressed={!customOpen && preset === item}
            onClick={() => {
              setCustomOpen(false);
              onChange(presetRange(item, today));
            }}
          >
            {rangePresetLabels[item]}
          </Button>
        ))}
        <Button
          type="button"
          size="sm"
          variant="ghost"
          className={cn("h-9", choiceClass(showFields, "end"))}
          aria-pressed={showFields}
          onClick={() => setCustomOpen(true)}
        >
          دلخواه
        </Button>
      </div>

      {showFields ? (
        <>
          <div className="w-full sm:w-64">
            <JalaliCalendarField
              label="از تاریخ"
              inputClassName={filterEdge}
              value={range.from ?? ""}
              onChange={(iso) => onChange({ ...range, from: iso === "" ? undefined : iso })}
            />
          </div>
          <div className="w-full sm:w-64">
            <JalaliCalendarField
              label="تا تاریخ"
              inputClassName={filterEdge}
              value={range.to ?? ""}
              error={error}
              onChange={(iso) => onChange({ ...range, to: iso === "" ? undefined : iso })}
            />
          </div>
        </>
      ) : (
        error !== undefined && <p className="w-full text-sm text-destructive">{error}</p>
      )}
    </div>
  );
}
