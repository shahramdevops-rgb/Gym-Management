import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { QueryClient } from "@tanstack/react-query";

import { guestDebtKeys } from "@/features/guestDebts/api";
import { lockerKeys } from "@/features/lockers/api";
import { memberKeys } from "@/features/members/api";
import { paymentKeys, type PaymentMethod } from "@/features/payments/api";
import { subscriptionKeys } from "@/features/subscriptions/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Attendance = components["schemas"]["AttendanceResponse"];
export type CurrentlyInside = components["schemas"]["CurrentlyInsideResponse"];
export type CheckInSale = components["schemas"]["CheckInSale"];

export const currentlyInsidePageSize = 20;
export const memberHistoryPageSize = 10;

/** How often the front desk board polls for who is inside (docs/ROADMAP.md 5.6: auto-refresh). */
export const currentlyInsideRefetchMs = 15_000;

/** Query keys. Every key starts with "attendance", like the members and plans keys. */
export const attendanceKeys = {
  all: ["attendance"] as const,
  currentlyInside: (page: number) => [...attendanceKeys.all, "currently-inside", page] as const,
  everyoneInside: () => [...attendanceKeys.all, "currently-inside", "all"] as const,
  memberHistory: (memberId: string, page: number) =>
    [...attendanceKeys.all, "history", memberId, page] as const,
  todayByHour: () => [...attendanceKeys.all, "today-by-hour"] as const,
};

/**
 * At most 72 lockers and 15 reserve places can be held at once (BUSINESS_RULES.md §6), so 87
 * open visits at most: one page of 100 always holds everyone inside.
 */
export const everyoneInsidePageSize = 100;

/** Everyone inside on one page, for the locker map to join to its lockers by number. */
export function useEveryoneInside() {
  return useQuery({
    queryKey: attendanceKeys.everyoneInside(),
    refetchInterval: currentlyInsideRefetchMs,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/attendance/currently-inside", {
        params: { query: { Page: 1, PageSize: everyoneInsidePageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data.items;
    },
  });
}

/**
 * The chart under the locker map changes an hour at a time, so a minute is often enough. A
 * check-in or cancel refreshes it at once anyway: its key starts with "attendance", which every
 * visit mutation invalidates.
 */
export const todayByHourRefetchMs = 60_000;

/** Today's check-ins by hour and the same weekday's average (BUSINESS_RULES.md §6 *Today by hour*). */
export function useTodayByHour() {
  return useQuery({
    queryKey: attendanceKeys.todayByHour(),
    refetchInterval: todayByHourRefetchMs,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/attendance/today-by-hour");
      if (error !== undefined) {
        throw error;
      }
      return {
        date: data.date,
        daysAveraged: Number(data.daysAveraged),
        hours: data.hours.map((row) => ({
          hour: Number(row.hour),
          today: Number(row.today),
          average: Number(row.average),
        })),
      };
    },
  });
}

export function useCurrentlyInside(page: number) {
  return useQuery({
    queryKey: attendanceKeys.currentlyInside(page),
    placeholderData: keepPreviousData,
    refetchInterval: currentlyInsideRefetchMs,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/attendance/currently-inside", {
        params: { query: { Page: page, PageSize: currentlyInsidePageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / currentlyInsidePageSize)),
      };
    },
  });
}

export function useMemberAttendanceHistory(memberId: string, page: number) {
  return useQuery({
    queryKey: attendanceKeys.memberHistory(memberId, page),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/members/{memberId}/attendance", {
        params: {
          path: { memberId },
          query: { Page: page, PageSize: memberHistoryPageSize },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / memberHistoryPageSize)),
      };
    },
  });
}

/**
 * Check-in, check-out and cancel all change the same four things: the front desk board, the
 * member's own history, locker occupancy, and the member's subscriptions (two different
 * features' caches, hence the cross-feature imports). Nothing here writes a single cache entry
 * the way member/plan mutations do — there is no "attendance detail" screen, only lists.
 *
 * Subscriptions are in the list because a visit changes them: every check-in consumes a session,
 * and a check-in against an exhausted subscription with a renewal queued behind it moves that
 * renewal forward to today (BUSINESS_RULES.md §4), which rewrites its dates.
 *
 * Member lists are in it because each row says whether that member is inside, which the search
 * screen and the map's check-in search both show.
 */
async function invalidateAttendance(queryClient: QueryClient) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: attendanceKeys.all }),
    queryClient.invalidateQueries({ queryKey: lockerKeys.all }),
    queryClient.invalidateQueries({ queryKey: subscriptionKeys.all }),
    queryClient.invalidateQueries({ queryKey: [...memberKeys.all, "list"] }),
    // A guest's check-out, cancel or «تسویه یکجا» changes what «بدهی مهمان‌ها» lists.
    queryClient.invalidateQueries({ queryKey: guestDebtKeys.all }),
  ]);
}

/**
 * Checks a member in with the locker the desk clicked, or a reserve place when `lockerId` is
 * `null` (BUSINESS_RULES.md §6, §7). Only the locker map calls this.
 *
 * With a `sale`, the API first sells a single visit or a plan and then checks the member in, in one
 * transaction: both happen or neither does, so a subscription sold at the locker always comes with
 * that locker (roadmap 6.5.7).
 */
export function useCheckIn() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      memberId,
      lockerId,
      sale,
    }: {
      memberId: string;
      lockerId: string | null;
      sale?: CheckInSale;
    }) => {
      const { data, error } = await api.POST("/api/members/{memberId}/attendance/check-in", {
        params: { path: { memberId } },
        body: sale === undefined ? { lockerId } : { lockerId, sale },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: (_attendance, { sale }) =>
      Promise.all([
        invalidateAttendance(queryClient),
        // A sale adds to what the member owes, which the member's own queries show.
        sale === undefined ? null : queryClient.invalidateQueries({ queryKey: memberKeys.all }),
      ]),
  });
}

/**
 * «ورود فقط هوازی» (BUSINESS_RULES.md §7 *Cardio-only visit*): a member with a plan comes in on the
 * locker the desk clicked, or a reserve place when `lockerId` is `null`, and no session is consumed.
 * Nothing is sold with it. The visit cannot be checked out until its هوازی amount is recorded.
 */
export function useCardioOnlyCheckIn() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ memberId, lockerId }: { memberId: string; lockerId: string | null }) => {
      const { data, error } = await api.POST(
        "/api/members/{memberId}/attendance/cardio-only-check-in",
        { params: { path: { memberId } }, body: { lockerId } },
      );
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: () => invalidateAttendance(queryClient),
  });
}

/**
 * Lets a guest in on the locker the desk clicked, or a reserve place when `lockerId` is `null`
 * (BUSINESS_RULES.md §7 *Guest visit*): a name and nothing else, no member, no session.
 */
export function useGuestCheckIn() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ guestName, lockerId }: { guestName: string; lockerId: string | null }) => {
      const { data, error } = await api.POST("/api/attendance/guest-check-in", {
        body: { guestName, lockerId },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: () => invalidateAttendance(queryClient),
  });
}

/**
 * «تسویه یکجا» for a guest: pays everything the visit still owes, cafe orders, هوازی and sales, one
 * payment per item (BUSINESS_RULES.md §7 *Guest visit*). `amount` is the total the box showed; the
 * API refuses it if anything changed since.
 */
export function useSettleGuestVisit() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      attendanceId,
      ...body
    }: {
      attendanceId: string;
      amount: string;
      method: PaymentMethod;
      referenceNumber: string | null;
    }) => {
      const { data, error } = await api.POST("/api/attendance/{id}/settle-guest", {
        params: { path: { id: attendanceId } },
        body,
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: () =>
      Promise.all([
        invalidateAttendance(queryClient),
        queryClient.invalidateQueries({ queryKey: paymentKeys.all }),
        // `cafeKeys.all`, spelled out: the cafe's api module imports this one.
        queryClient.invalidateQueries({ queryKey: ["cafe"] }),
      ]),
  });
}

export function useCheckOut() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (attendanceId: string) => {
      const { data, error } = await api.POST("/api/attendance/{id}/check-out", {
        params: { path: { id: attendanceId } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: () => invalidateAttendance(queryClient),
  });
}

/** Moves an open visit to another free locker (BUSINESS_RULES.md §7 *Moving to another locker*). */
export function useMoveLocker() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ attendanceId, lockerId }: { attendanceId: string; lockerId: string }) => {
      const { data, error } = await api.POST("/api/attendance/{id}/move-locker", {
        params: { path: { id: attendanceId } },
        body: { lockerId },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: () => invalidateAttendance(queryClient),
  });
}

/** What the desk ticked in the cancel box: the visit's هوازی, and each cafe order and sale (فروشگاه, آنالیز) on its own. */
export interface CancelCheckInChoice {
  attendanceId: string;
  voidCardio: boolean;
  cafeOrderIds: string[];
  saleIds: string[];
}

/**
 * Cancels a check-in, and with it the purchases the desk ticked (BUSINESS_RULES.md §7 *Cancel
 * check-in*, roadmap 6.5.8). The choice is always sent in full: the API refuses a request without
 * it rather than guess.
 */
export function useCancelCheckIn() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      attendanceId,
      voidCardio,
      cafeOrderIds,
      saleIds,
    }: CancelCheckInChoice) => {
      const { data, error } = await api.POST("/api/attendance/{id}/cancel", {
        params: { path: { id: attendanceId } },
        body: { voidCardio, cafeOrderIds, saleIds },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: () =>
      Promise.all([
        invalidateAttendance(queryClient),
        // A cancelled purchase leaves the member's debt and may write refunds.
        queryClient.invalidateQueries({ queryKey: memberKeys.all }),
        queryClient.invalidateQueries({ queryKey: paymentKeys.all }),
        // `cafeKeys.all`, spelled out: the cafe's api module imports this one.
        queryClient.invalidateQueries({ queryKey: ["cafe"] }),
      ]),
  });
}
