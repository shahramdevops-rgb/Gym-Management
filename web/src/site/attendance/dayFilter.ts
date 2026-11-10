import type { SessionPresence } from "./api";

/** What a count box stands for, and how the day page filters its students by it (15.7f). */
export type DayFilter = "students" | "present" | "late" | "cameAfter" | "absent";

const filterStates: Record<DayFilter, readonly SessionPresence[]> = {
  // The students the counts count: everyone in a session except «مرخصی» and «لغو شد».
  students: ["NotYet", "OnTime", "Late", "CameAfter", "Absent"],
  present: ["OnTime", "Late"],
  late: ["Late"],
  cameAfter: ["CameAfter"],
  absent: ["Absent"],
};

/** Whether a student's state belongs to the box `filter`. */
export function inFilter(state: SessionPresence, filter: DayFilter): boolean {
  return filterStates[filter].includes(state);
}

/** The `?show=` of the day page's address as a filter; anything else is no filter. */
export function parseDayFilter(value: string | null): DayFilter | null {
  return value !== null && Object.hasOwn(filterStates, value) ? (value as DayFilter) : null;
}
