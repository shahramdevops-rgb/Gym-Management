import { toPersianDigits } from "@/lib/format";

import type { DayTotals } from "./api";

/**
 * A day's counts in five small boxes (BUSINESS_RULES.md §15 *More for the trainer*, 15.7d):
 * students, present (late and early included), late, came after the session (neither present nor
 * absent), absent; «مرخصی» and «لغو شد» are in none.
 * On the day page and in the home's «امروز», both for now: the developer will say which to keep.
 */
export function DayTotalsBoxes({ totals, label }: { totals: DayTotals; label: string }) {
  const boxes = [
    { name: "شاگردان", count: totals.students },
    { name: "حاضر", count: totals.present },
    { name: "با تأخیر", count: totals.late },
    { name: "بعد از سانس", count: totals.cameAfter },
    { name: "غایب", count: totals.absent },
  ];

  return (
    <ul aria-label={label} className="grid grid-cols-5 gap-1.5">
      {boxes.map((box) => (
        <li key={box.name} className="rounded-lg bg-muted px-1 py-2 text-center">
          <p className="text-lg font-bold">{toPersianDigits(box.count)}</p>
          <p className="text-xs text-muted-foreground">{box.name}</p>
        </li>
      ))}
    </ul>
  );
}
