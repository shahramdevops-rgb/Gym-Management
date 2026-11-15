import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type { components } from "@/lib/api/schema";

import { siteApi } from "../api/client";
import { siteMeQueryKey } from "../auth/api";
import { studentsQueryKey } from "../coaching/api";
import { bookingsQueryKey, timetableQueryKey, type DayOfWeek } from "../timetable/api";

export type StudentSchedule = components["schemas"]["StudentScheduleResponse"];
export type FixedSchedule = components["schemas"]["FixedScheduleResponse"];
export type Place = components["schemas"]["PlaceResponse"];
export type SessionStudents = components["schemas"]["SessionStudentsResponse"];
export type SessionStudentsItem = components["schemas"]["SessionStudentsItem"];
export type MySessions = components["schemas"]["MySessionsResponse"];
export type Opening = components["schemas"]["OpeningResponse"];
export type SessionDate = components["schemas"]["SessionDateResponse"];
export type MyCancellations = components["schemas"]["MyCancellationsResponse"];
export type LeaveRequests = components["schemas"]["LeaveRequestsResponse"];
export type LeaveRequestItem = components["schemas"]["LeaveRequestItem"];

const studentScheduleKey = (memberId: string) => [...bookingsQueryKey, "student", memberId];
const sessionStudentsKey = (date: string) => [...bookingsQueryKey, "day", date];
export const mySessionsQueryKey = [...bookingsQueryKey, "mine"] as const;
export const myCancellationsQueryKey = [...bookingsQueryKey, "cancelled"] as const;
export const leaveRequestsQueryKey = [...bookingsQueryKey, "leave-requests"] as const;

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

/** `alsoChanged`: another read the change touches, outside the bookings' keys, read again too. */
function useBookingChange<TVariables, TData>(
  send: (variables: TVariables) => Promise<TData>,
  key: readonly unknown[],
  alsoChanged?: readonly unknown[],
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
      if (alsoChanged !== undefined) {
        void queryClient.invalidateQueries({ queryKey: alsoChanged });
      }
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

/**
 * «تمدید» of the coaching period, or the days left of a student from before it (roadmap 15.7h). It
 * answers with the student's «برنامهٔ ثابت», which the days left may have cut; «شاگردان من» shows the
 * period too, so it is read again.
 */
export function useRenewCoachingPeriod(memberId: string) {
  return useBookingChange(
    async (days: number) => {
      const { data, error } = await siteApi.POST("/api/site/coaching/students/{memberId}/period", {
        params: { path: { memberId } },
        body: { days },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    studentScheduleKey(memberId),
    studentsQueryKey,
  );
}

/**
 * «ویرایش دوره» (15.7k): the days left of a running period, from today, earlier or later. Answers
 * with the student's «برنامهٔ ثابت», which an earlier last day may have cut; «شاگردان من» shows the
 * period too, so it is read again.
 */
export function useSetCoachingDaysLeft(memberId: string) {
  return useBookingChange(
    async (daysLeft: number) => {
      const { data, error } = await siteApi.POST(
        "/api/site/coaching/students/{memberId}/period/days-left",
        { params: { path: { memberId } }, body: { daysLeft } },
      );
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    studentScheduleKey(memberId),
    studentsQueryKey,
  );
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

/**
 * «یادداشت این روز» (roadmap 15.7e): the note on one session of the date, written or changed. The
 * timetable and every student's dates are read again, since they show it too.
 */
export function useSetSessionNote(date: string) {
  return useBookingChange(async (note: { start: string; end: string; text: string }) => {
    const { data, error } = await siteApi.PUT("/api/site/timetable/days/{date}/note", {
      params: { path: { date } },
      body: note,
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, sessionStudentsKey(date));
}

/** «پاک کردن یادداشت»: the note on one session of the date goes. */
export function useRemoveSessionNote(date: string) {
  return useBookingChange(async (session: { start: string; end: string }) => {
    const { data, error } = await siteApi.POST("/api/site/timetable/days/{date}/note/remove", {
      params: { path: { date } },
      body: session,
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

/**
 * «درخواست مرخصی» (roadmap 15.7i): asks the trainer for leave from one date's session, by the day
 * before; the member stays in it until the trainer accepts. Times as the API gave them.
 */
export function useLeaveMySession() {
  return useBookingChange(async (place: { date: string; start: string; end: string }) => {
    const { data, error } = await siteApi.POST("/api/site/me/sessions/leave", { body: place });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, mySessionsQueryKey);
}

/** «لغو درخواست»: a leave request the trainer has not answered (15.7i). */
export function useCancelMyLeave() {
  return useBookingChange(async (place: { date: string; start: string; end: string }) => {
    const { data, error } = await siteApi.POST("/api/site/me/sessions/leave/cancel", {
      body: place,
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, mySessionsQueryKey);
}

/** «می‌آیم»: back in a date the member has leave from, until it starts, while it has room. */
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

// ---- The member: «سانس لغو شد» (roadmap 15.6c) ----

/** The sessions the member's trainer cancelled that they have not pressed «دیدم» for. */
export function useMyCancellations() {
  return useQuery({
    queryKey: myCancellationsQueryKey,
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/me/sessions/cancelled");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/**
 * «دیدم»: every cancellation up to `upTo`, the `latest` the member read, is seen; one saved after it
 * stays on the home.
 */
export function useSeeMyCancellations() {
  return useBookingChange(async (upTo: string) => {
    const { data, error } = await siteApi.POST("/api/site/me/sessions/cancelled/seen", {
      body: { upTo },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, myCancellationsQueryKey);
}

// ---- The trainer: «درخواست‌های مرخصی» (roadmap 15.7i) ----

export function useLeaveRequests() {
  return useQuery({
    queryKey: leaveRequestsQueryKey,
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/coaching/leave-requests");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/**
 * «قبول» or «رد». Accepting frees the student's place: the timetable's counts and the day's
 * attendance (under the bookings' key) are read again.
 */
export function useAnswerLeaveRequest() {
  return useBookingChange(async ({ requestId, accept }: { requestId: string; accept: boolean }) => {
    const { data, error } = accept
      ? await siteApi.POST("/api/site/coaching/leave-requests/{id}/accept", {
          params: { path: { id: requestId } },
        })
      : await siteApi.POST("/api/site/coaching/leave-requests/{id}/decline", {
          params: { path: { id: requestId } },
        });
    if (error !== undefined) {
      throw error;
    }
    return data;
  }, leaveRequestsQueryKey);
}
