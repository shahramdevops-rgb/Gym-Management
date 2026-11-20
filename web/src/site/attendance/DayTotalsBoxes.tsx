import { Link } from "react-router";

import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { DayTotals } from "./api";
import type { DayFilter } from "./dayFilter";
import { presenceVariants } from "./labels";
import { boxTones, toneOf, type BoxTone } from "./tones";

/**
 * A day's counts in five small boxes (BUSINESS_RULES.md §15 *More for the trainer*, 15.7d):
 * students, present (late and early included), late, «خارج از سانس» (came that day at another
 * hour, neither present nor absent; 15.7p, in place of «بعد از سانس»), absent; «مرخصی» and «لغو
 * شد» are in none.
 * On the day page and in the home's «امروز», both for now: the developer will say which to keep.
 * Each box opens its students (15.7f): on the home a link (`linkTo`) to the day page filtered to
 * it; on the day page a toggle (`onSelect`, `selected`) that filters the sessions in place.
 * Each box has its state's colour (15.7p, asked by the developer), «شاگردان» neutral; a pressed box
 * fills with it.
 */
export function DayTotalsBoxes({
  totals,
  label,
  selected = null,
  onSelect,
  linkTo,
}: {
  totals: DayTotals;
  label: string;
  selected?: DayFilter | null;
  onSelect?: (filter: DayFilter) => void;
  linkTo?: (filter: DayFilter) => string;
}) {
  const boxes: { filter: DayFilter; name: string; count: number | string; tone: BoxTone }[] = [
    { filter: "students", name: "شاگردان", count: totals.students, tone: "neutral" },
    {
      filter: "present",
      name: "حاضر",
      count: totals.present,
      tone: toneOf(presenceVariants.OnTime),
    },
    { filter: "late", name: "با تأخیر", count: totals.late, tone: toneOf(presenceVariants.Late) },
    {
      filter: "outsideSession",
      name: "خارج از سانس",
      count: totals.outsideSession,
      tone: toneOf(presenceVariants.OutsideSession),
    },
    {
      filter: "absent",
      name: "غایب",
      count: totals.absent,
      tone: toneOf(presenceVariants.Absent),
    },
  ];

  const opens = onSelect !== undefined || linkTo !== undefined;

  const boxClass = (tone: BoxTone, isSelected: boolean) =>
    cn(
      "block h-full w-full rounded-lg px-1 py-2 text-center transition-colors",
      isSelected ? boxTones[tone].filled : boxTones[tone].box,
      opens && !isSelected && boxTones[tone].hover,
    );

  return (
    <ul aria-label={label} className="grid grid-cols-5 gap-1.5">
      {boxes.map((box) => {
        const isSelected = box.filter === selected;
        const content = (
          <>
            <span
              className={cn("block text-lg font-bold", !isSelected && boxTones[box.tone].count)}
            >
              {toPersianDigits(box.count)}
            </span>
            <span className={cn("block text-xs", !isSelected && "text-muted-foreground")}>
              {box.name}
            </span>
          </>
        );

        return (
          <li key={box.filter}>
            {linkTo !== undefined ? (
              <Link to={linkTo(box.filter)} className={boxClass(box.tone, false)}>
                {content}
              </Link>
            ) : onSelect !== undefined ? (
              <button
                type="button"
                aria-pressed={isSelected}
                className={boxClass(box.tone, isSelected)}
                onClick={() => onSelect(box.filter)}
              >
                {content}
              </button>
            ) : (
              <div className={boxClass(box.tone, false)}>{content}</div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
