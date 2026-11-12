import { useQuery } from "@tanstack/react-query";

import type { components } from "@/lib/api/schema";

import { siteApi } from "../api/client";
import { bookingsQueryKey } from "../timetable/api";

export type TrainerDay = components["schemas"]["TrainerDayResponse"];
export type DaySession = components["schemas"]["DaySession"];
export type DayStudent = components["schemas"]["DayStudent"];
export type DayTotals = components["schemas"]["DayTotals"];
export type OutsideStudent = components["schemas"]["OutsideStudent"];
export type VisitTimes = components["schemas"]["VisitTimes"];
export type StudentAttendance = components["schemas"]["StudentAttendanceResponse"];
export type AttendanceItem = components["schemas"]["AttendanceItem"];
export type OutsideVisit = components["schemas"]["OutsideVisit"];
export type SessionPresence = components["schemas"]["SessionPresence"];

// Under the bookings' key: a change of a schedule or of the timetable reads them again.
const trainerTodayKey = [...bookingsQueryKey, "today"] as const;
const trainerDayKey = (date: string) => [...bookingsQueryKey, "day", date] as const;
const studentAttendanceKey = (memberId: string, from: string) =>
  [...bookingsQueryKey, "attendance", memberId, from] as const;

/** «حضور امروز» asks again every minute while it is open (BUSINESS_RULES.md §15 *Attendance for the trainer*). */
export const todayRefreshMs = 60_000;

/**
 * The trainer's sessions today with each student's state from the desk's check-ins (roadmap
 * 15.7). The day page passes `refresh`; the home's cards read it once, as the home's other cards.
 */
export function useTrainerToday({
  refresh = false,
  enabled = true,
}: { refresh?: boolean; enabled?: boolean } = {}) {
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
    enabled,
  });
}

/**
 * An earlier day's attendance (roadmap 15.7d), from the day page's «روز قبل» or its date box.
 * It does not refresh itself: its sessions are over.
 */
export function useTrainerDay(date: string, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: trainerDayKey(date),
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/coaching/days/{date}", {
        params: { path: { date } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    enabled,
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

const myAttendanceKey = (from: string) => [...bookingsQueryKey, "my-attendance", from] as const;

/** «حضور من» (roadmap 15.7j): the member's own sessions from `from` to `to`, and the totals. */
export function useMyAttendance(from: string, to: string) {
  return useQuery({
    queryKey: myAttendanceKey(from),
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/me/attendance", {
        params: { query: { from, to } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}
