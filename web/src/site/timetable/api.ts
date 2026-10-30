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

/** A session as the editor sends it: times as `HH:mm`, which the API reads as `HH:mm:00`. */
export interface SessionInput {
  start: string;
  end: string;
  capacity: number;
}

const withSeconds = (time: string) => `${time}:00`;

const toBody = (session: SessionInput) => ({
  start: withSeconds(session.start),
  end: withSeconds(session.end),
  capacity: session.capacity,
});

/*
 * The trainer's «برنامه سانس‌ها» (BUSINESS_RULES.md §15 *The trainer's sessions*, roadmap 15.6a).
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
    onSuccess: (timetable) => queryClient.setQueryData(timetableQueryKey, timetable),
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: timetableQueryKey });
      void queryClient.invalidateQueries({ queryKey: siteMeQueryKey });
    },
  });
}

/** «ذخیره الگو»: the week's sessions from a date on. */
export function useSetWeekPattern() {
  return useTimetableChange(
    async ({
      startsOn,
      sessions,
    }: {
      startsOn: string;
      sessions: (SessionInput & { day: DayOfWeek })[];
    }) => {
      const { data, error } = await siteApi.PUT("/api/site/timetable/pattern", {
        body: {
          startsOn,
          sessions: sessions.map((session) => ({ day: session.day, ...toBody(session) })),
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}

/** «ذخیره» for one date on its own; no sessions closes the day. */
export function useSetTimetableDay() {
  return useTimetableChange(
    async ({ date, sessions }: { date: string; sessions: SessionInput[] }) => {
      const { data, error } = await siteApi.PUT("/api/site/timetable/days/{date}", {
        params: { path: { date } },
        body: { sessions: sessions.map(toBody) },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
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
