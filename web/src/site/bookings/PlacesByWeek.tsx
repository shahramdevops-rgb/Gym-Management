import { useState, type ReactNode } from "react";

import { addDays, daysBetween } from "../timetable/labels";
import { WeekNav } from "../timetable/WeekNav";
import type { Place } from "./api";

/**
 * A student's dates one week at a time, as «روزهای پیش رو» shows the timetable (the developer,
 * 1405/08/09: a program of eight weeks is too long a list on a phone). The weeks are counted from
 * today and reach the last date; `children` draws one week's dates.
 */
export function PlacesByWeek({
  today,
  places,
  empty,
  children,
}: {
  today: string;
  /** From today on, by date. */
  places: Place[];
  /** Said when there is no date at all. */
  empty: string;
  children: (week: Place[]) => ReactNode;
}) {
  const [index, setIndex] = useState(0);
  const last = places.at(-1);

  if (last === undefined) {
    return <p className="text-sm text-muted-foreground">{empty}</p>;
  }

  const count = Math.floor(daysBetween(today, last.date) / 7) + 1;
  // A week left behind when its last date goes, by a leave or the trainer's change.
  const shown = Math.min(index, count - 1);
  const from = addDays(today, shown * 7);
  const to = addDays(from, 6);
  const week = places.filter((place) => place.date >= from && place.date <= to);

  return (
    <div className="space-y-3">
      <WeekNav index={shown} count={count} from={from} to={to} onChange={setIndex} />
      {week.length === 0 ? (
        <p className="text-sm text-muted-foreground">این هفته سانسی نیست.</p>
      ) : (
        children(week)
      )}
    </div>
  );
}
