import { useQuery } from "@tanstack/react-query";

import type { components } from "@/lib/api/schema";

import { siteApi } from "../api/client";
import { bookingsQueryKey } from "../timetable/api";

export type TrainerToday = components["schemas"]["TrainerTodayResponse"];
export type TodaySession = components["schemas"]["TodaySession"];
export type TodayStudent = components["schemas"]["TodayStudent"];
export type StudentAttendance = components["schemas"]["StudentAttendanceResponse"];
export type AttendanceItem = components["schemas"]["AttendanceItem"];
export type SessionPresence = components["schemas"]["SessionPresence"];

// Under the bookings' key: a change of a schedule or of the timetable reads them again.
const trainerTodayKey = [...bookingsQueryKey, "today"] as const;
const studentAttendanceKey = (memberId: string, from: string) =>
  [...bookingsQueryKey, "attendance", memberId, from] as const;

/** «حضور امروز» asks again every minute while it is open (BUSINESS_RULES.md §15 *Attendance for the trainer*). */
export const todayRefreshMs = 60_000;

/**
 * The trainer's sessions today with each student's state from the desk's check-ins (roadmap
 * 15.7). The page passes `refresh`; the home's «غایبان امروز» reads it once, as the home's other cards.
 */
export function useTrainerToday({ refresh = false }: { refresh?: boolean } = {}) {
  return useQuery({
    queryKey: trainerTodayKey,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/coaching/today");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    refetchInterval: refresh ? todayRefreshMs : false,
  });
}

/** A student's sessions from `from` to `to` (a Jalali month), each with its state, and the totals. */
export function useStudentAttendance(memberId: string, from: string, to: string) {
  return useQuery({
    queryKey: studentAttendanceKey(memberId, from),
    queryFn: async () => {
      const { data, error } = await siteApi.GET(
        "/api/site/coaching/students/{memberId}/attendance",
        { params: { path: { memberId }, query: { from, to } } },
      );
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}
