import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type { components } from "@/lib/api/schema";

import { siteApi } from "../api/client";
import { siteMeQueryKey } from "../auth/api";
import { bookingsQueryKey, timetableQueryKey, type DayOfWeek } from "../timetable/api";

export type StudentSchedule = components["schemas"]["StudentScheduleResponse"];
export type FixedSchedule = components["schemas"]["FixedScheduleResponse"];
export type Place = components["schemas"]["PlaceResponse"];
export type SessionStudents = components["schemas"]["SessionStudentsResponse"];
export type SessionStudentsItem = components["schemas"]["SessionStudentsItem"];
export type MySessions = components["schemas"]["MySessionsResponse"];
export type Opening = components["schemas"]["OpeningResponse"];
export type SessionDate = components["schemas"]["SessionDateResponse"];

const studentScheduleKey = (memberId: string) => [...bookingsQueryKey, "student", memberId];
const sessionStudentsKey = (date: string) => [...bookingsQueryKey, "day", date];
export const mySessionsQueryKey = [...bookingsQueryKey, "mine"] as const;

/** A weekly session as the form sends it: times as `HH:mm`, which the API reads as `HH:mm:00`. */
export interface SlotInput {
  day: DayOfWeek;
  start: string;
  end: string;
}

export interface ScheduleInput {
  startsOn: string;
  endsOn: string;
  slots: SlotInput[];
}

const withSeconds = (time: string) => `${time}:00`;

const scheduleBody = ({ startsOn, endsOn, slots }: ScheduleInput) => ({
  startsOn,
  endsOn,
  slots: slots.map((slot) => ({
    day: slot.day,
    start: withSeconds(slot.start),
    end: withSeconds(slot.end),
  })),
});

/*
 * Students in the trainer's sessions (BUSINESS_RULES.md §15 *Students in sessions*, roadmap 15.6b).
 * Every change answers with what it changed as it is after it, which replaces the cached one; the
 * timetable's student counts and the other reads are read again. A refused change also reads «me»
 * again, since the link may have ended or the trainer been stopped meanwhile.
 */

function useBookingChange<TVariables, TData>(
  send: (variables: TVariables) => Promise<TData>,
  key: readonly unknown[],
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: send,
    onSuccess: (data) => {
      queryClient.setQueryData(key, data);
      void queryClient.invalidateQueries({
        queryKey: bookingsQueryKey,
        predicate: (query) => query.queryKey.join() !== key.join(),
      });
      void queryClient.invalidateQueries({ queryKey: timetableQueryKey });
    },
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: bookingsQueryKey });
      void queryClient.invalidateQueries({ queryKey: timetableQueryKey });
      void queryClient.invalidateQueries({ queryKey: siteMeQueryKey });
    },
  });
}

// ---- The trainer: a student's «برنامهٔ ثابت» ----

export function useStudentSchedule(memberId: string) {
  return useQuery({
    queryKey: studentScheduleKey(memberId),
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/coaching/students/{memberId}/schedule", {
        params: { path: { memberId } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** What «ذخیره برنامه» would do: how many dates, and those with no room. Nothing is kept. */
export function useCheckStudentSchedule(memberId: string) {
  return useMutation({
    mutationFn: async (schedule: ScheduleInput) => {
      const { data, error } = await siteApi.POST(
        "/api/site/coaching/students/{memberId}/schedule/check",
        { params: { path: { memberId } }, body: scheduleBody(schedule) },
      );
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** «ذخیره برنامه»: saved whole, or not at all when one of its dates has no room. */
export function useSetStudentSchedule(memberId: string) {
  return useBookingChange(async (schedule: ScheduleInput) => {
    const { data, error } = await siteApi.PUT("/api/site/coaching/students/{memberId}/schedule", {
      params: { path: { memberId } },
      body: scheduleBody(schedule),
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, studentScheduleKey(memberId));
}

/** «پایان برنامه»: from now; today's sessions that have started stay. */
export function useEndStudentSchedule(memberId: string) {
  return useBookingChange(async () => {
    const { data, error } = await siteApi.DELETE(
      "/api/site/coaching/students/{memberId}/schedule",
      { params: { path: { memberId } } },
    );
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, studentScheduleKey(memberId));
}

// ---- The trainer: «شاگردان سانس» of one date ----

export function useSessionStudents(date: string) {
  return useQuery({
    queryKey: sessionStudentsKey(date),
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/timetable/days/{date}/students", {
        params: { path: { date } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** «حذف از این روز»: one student out of one session of the date. */
export function useRemoveSessionStudent(date: string) {
  return useBookingChange(async (place: { memberId: string; start: string; end: string }) => {
    const { data, error } = await siteApi.POST("/api/site/timetable/days/{date}/students/remove", {
      params: { path: { date } },
      body: place,
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, sessionStudentsKey(date));
}

// ---- The member: «سانس‌های من» ----

export function useMySessions() {
  return useQuery({
    queryKey: mySessionsQueryKey,
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/me/sessions");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** «نمی‌آیم»: out of one date's session, which frees the place. Times as the API gave them. */
export function useLeaveMySession() {
  return useBookingChange(async (place: { date: string; start: string; end: string }) => {
    const { data, error } = await siteApi.POST("/api/site/me/sessions/leave", { body: place });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, mySessionsQueryKey);
}

/** «می‌آیم»: back in a date the member left, until it starts, while it has room. */
export function useReturnToMySession() {
  return useBookingChange(async (place: { date: string; start: string; end: string }) => {
    const { data, error } = await siteApi.POST("/api/site/me/sessions/return", { body: place });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, mySessionsQueryKey);
}

/** «جابه‌جایی»: one date's session to another session of the same trainer with room. */
export function useMoveMySession() {
  return useBookingChange(async ({ from, to }: { from: Place; to: Opening }) => {
    const { data, error } = await siteApi.POST("/api/site/me/sessions/move", {
      body: {
        date: from.date,
        start: from.start,
        end: from.end,
        toDate: to.date,
        toStart: to.start,
        toEnd: to.end,
      },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, mySessionsQueryKey);
}
