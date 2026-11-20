import type { SessionPresence } from "./api";

/** What a count box stands for, and how the day page filters its students by it (15.7f). */
export type DayFilter = "students" | "present" | "late" | "outsideSession" | "absent";

const filterStates: Record<DayFilter, readonly SessionPresence[]> = {
  // The students the counts count: everyone in a session except «مرخصی» and «لغو شد».
  students: ["NotYet", "Early", "OnTime", "Late", "OutsideSession", "Absent"],
  // Early, on time and late all came to the session (15.7m).
  present: ["Early", "OnTime", "Late"],
  late: ["Late"],
  // Came that day at another hour (15.7p, in place of «بعد از سانس»).
  outsideSession: ["OutsideSession"],
  absent: ["Absent"],
};

/** Whether a student's state belongs to the box `filter`. */
export function inFilter(state: SessionPresence, filter: DayFilter): boolean {
  return filterStates[filter].includes(state);
}

/**
 * The `?show=` of the day page's address as a filter; anything else is no filter (an old
 * `?show=cameAfter`, from before 15.7p, shows everyone).
 */
export function parseDayFilter(value: string | null): DayFilter | null {
  return value !== null && Object.hasOwn(filterStates, value) ? (value as DayFilter) : null;
}
