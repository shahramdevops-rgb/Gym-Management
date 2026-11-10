import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type { components } from "@/lib/api/schema";

import { siteApi } from "../api/client";
import { siteMeQueryKey } from "../auth/api";

export type Timetable = components["schemas"]["TimetableResponse"];
export type TimetableDay = components["schemas"]["TimetableDayResponse"];
export type Session = components["schemas"]["SessionResponse"];
export type WeeklySession = components["schemas"]["WeeklySessionResponse"];
export type DayOfWeek = components["schemas"]["DayOfWeek"];

export const timetableQueryKey = ["site", "timetable"] as const;
/** Every read of students in sessions (15.6b); each one's own key starts with it. */
export const bookingsQueryKey = ["site", "bookings"] as const;

/** A session as the editor sends it: times as `HH:mm`, which the API reads as `HH:mm:00`. */
export interface SessionInput {
  start: string;
  end: string;
  capacity: number;
  /** Marked VIP by the trainer (15.6e): for now only a label. */
  isVip: boolean;
  /** The standing note («یادداشت ثابت», 15.7e), as typed; blank is none. */
  note: string;
}

const withSeconds = (time: string) => `${time}:00`;

const toBody = (session: SessionInput) => ({
  start: withSeconds(session.start),
  end: withSeconds(session.end),
  capacity: session.capacity,
  isVip: session.isVip,
  note: session.note.trim() === "" ? null : session.note.trim(),
});

/*
 * The trainer's «تمام سانس‌های من» (BUSINESS_RULES.md §15 *The trainer's sessions*, roadmap 15.6a).
 * Every change answers with the whole timetable as it is after it, which replaces the cached one;
 * a refused change reads it and «me» again, since the Owner may have stopped the trainer.
 */

export function useTimetable() {
  return useQuery({
    queryKey: timetableQueryKey,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/timetable");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

function useTimetableChange<TVariables>(send: (variables: TVariables) => Promise<Timetable>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: send,
    onSuccess: (timetable) => {
      queryClient.setQueryData(timetableQueryKey, timetable);
      // The students' places follow the sessions: a removed one shows as «لغو شد».
      void queryClient.invalidateQueries({ queryKey: bookingsQueryKey });
    },
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: timetableQueryKey });
      void queryClient.invalidateQueries({ queryKey: siteMeQueryKey });
    },
  });
}

export interface PatternInput {
  startsOn: string;
  sessions: (SessionInput & { day: DayOfWeek })[];
}

export interface DayInput {
  date: string;
  sessions: SessionInput[];
}

const patternBody = ({ startsOn, sessions }: PatternInput) => ({
  startsOn,
  sessions: sessions.map((session) => ({ day: session.day, ...toBody(session) })),
});

/** «ذخیره الگو»: the week's sessions from a date on. */
export function useSetWeekPattern() {
  return useTimetableChange(async (pattern: PatternInput) => {
    const { data, error } = await siteApi.PUT("/api/site/timetable/pattern", {
      body: patternBody(pattern),
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

/** «ذخیره» for one date on its own; no sessions closes the day. */
export function useSetTimetableDay() {
  return useTimetableChange(async ({ date, sessions }: DayInput) => {
    const { data, error } = await siteApi.PUT("/api/site/timetable/days/{date}", {
      params: { path: { date } },
      body: { sessions: sessions.map(toBody) },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

/** «برگرداندن به الگو». */
export function useResetTimetableDay() {
  return useTimetableChange(async (date: string) => {
    const { data, error } = await siteApi.DELETE("/api/site/timetable/days/{date}", {
      params: { path: { date } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

// ---- The warning before a change (BUSINESS_RULES.md §15 *Students in sessions*, 15.6b) ----

export type TimetableImpact = components["schemas"]["TimetableImpactResponse"];
export type StudentToReschedule = components["schemas"]["StudentToRescheduleResponse"];

/** Whether a checked change touches no student and can be saved without asking. */
export const touchesNoOne = (impact: TimetableImpact) => Number(impact.students) === 0;

/*
 * What a change would do to the students in the sessions it touches, with nothing kept. The
 * trainer is warned when it touches any, and saves anyway or not; a check never refuses.
 */

export function useCheckWeekPattern() {
  return useMutation({
    mutationFn: async (pattern: PatternInput) => {
      const { data, error } = await siteApi.POST("/api/site/timetable/pattern/check", {
        body: patternBody(pattern),
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

export function useCheckTimetableDay() {
  return useMutation({
    mutationFn: async ({ date, sessions }: DayInput) => {
      const { data, error } = await siteApi.POST("/api/site/timetable/days/{date}/check", {
        params: { path: { date } },
        body: { sessions: sessions.map(toBody) },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

export function useCheckResetTimetableDay() {
  return useMutation({
    mutationFn: async (date: string) => {
      const { data, error } = await siteApi.GET("/api/site/timetable/days/{date}/reset-check", {
        params: { path: { date } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}
