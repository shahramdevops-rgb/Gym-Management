import { Link } from "react-router";

import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import type { DayTotals } from "./api";
import type { DayFilter } from "./dayFilter";

/**
 * A day's counts in five small boxes (BUSINESS_RULES.md §15 *More for the trainer*, 15.7d):
 * students, present (late and early included), late, came after the session (neither present nor
 * absent), absent; «مرخصی» and «لغو شد» are in none.
 * On the day page and in the home's «امروز», both for now: the developer will say which to keep.
 * Each box opens its students (15.7f): on the home a link (`linkTo`) to the day page filtered to
 * it; on the day page a toggle (`onSelect`, `selected`) that filters the sessions in place.
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
  const boxes: { filter: DayFilter; name: string; count: number | string }[] = [
    { filter: "students", name: "شاگردان", count: totals.students },
    { filter: "present", name: "حاضر", count: totals.present },
    { filter: "late", name: "با تأخیر", count: totals.late },
    { filter: "cameAfter", name: "بعد از سانس", count: totals.cameAfter },
    { filter: "absent", name: "غایب", count: totals.absent },
  ];

  const boxClass = (isSelected: boolean) =>
    cn(
      "block h-full w-full rounded-lg px-1 py-2 text-center",
      isSelected ? "bg-primary text-primary-foreground" : "bg-muted",
      (onSelect !== undefined || linkTo !== undefined) && "transition-colors hover:bg-accent",
      isSelected && "hover:bg-primary/90",
    );

  return (
    <ul aria-label={label} className="grid grid-cols-5 gap-1.5">
      {boxes.map((box) => {
        const isSelected = box.filter === selected;
        const content = (
          <>
            <span className="block text-lg font-bold">{toPersianDigits(box.count)}</span>
            <span
              className={cn(
                "block text-xs",
                isSelected ? "text-primary-foreground" : "text-muted-foreground",
              )}
            >
              {box.name}
            </span>
          </>
        );

        return (
          <li key={box.filter}>
            {linkTo !== undefined ? (
              <Link to={linkTo(box.filter)} className={boxClass(false)}>
                {content}
              </Link>
            ) : onSelect !== undefined ? (
              <button
                type="button"
                aria-pressed={isSelected}
                className={boxClass(isSelected)}
                onClick={() => onSelect(box.filter)}
              >
                {content}
              </button>
            ) : (
              <div className={boxClass(false)}>{content}</div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
