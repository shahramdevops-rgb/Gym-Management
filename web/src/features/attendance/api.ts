import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { QueryClient } from "@tanstack/react-query";

import { lockerKeys } from "@/features/lockers/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Attendance = components["schemas"]["AttendanceResponse"];
export type CurrentlyInside = components["schemas"]["CurrentlyInsideResponse"];

export const currentlyInsidePageSize = 20;
export const memberHistoryPageSize = 10;

/** How often the front desk board polls for who is inside (docs/ROADMAP.md 5.6: auto-refresh). */
export const currentlyInsideRefetchMs = 15_000;

/** Query keys. Every key starts with "attendance", like the members and plans keys. */
export const attendanceKeys = {
  all: ["attendance"] as const,
  currentlyInside: (page: number) => [...attendanceKeys.all, "currently-inside", page] as const,
  memberHistory: (memberId: string, page: number) =>
    [...attendanceKeys.all, "history", memberId, page] as const,
};

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
 * Check-in, check-out and cancel all change the same three things: the front desk board, the
 * member's own history, and locker occupancy (a different feature's cache, hence the
 * cross-feature import). Nothing here writes a single cache entry the way member/plan mutations
 * do — there is no "attendance detail" screen, only lists.
 */
async function invalidateAttendance(queryClient: QueryClient) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: attendanceKeys.all }),
    queryClient.invalidateQueries({ queryKey: lockerKeys.all }),
  ]);
}

export function useCheckIn() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (memberId: string) => {
      const { data, error } = await api.POST("/api/members/{memberId}/attendance/check-in", {
        params: { path: { memberId } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: () => invalidateAttendance(queryClient),
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

export function useCancelCheckIn() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (attendanceId: string) => {
      const { data, error } = await api.POST("/api/attendance/{id}/cancel", {
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
