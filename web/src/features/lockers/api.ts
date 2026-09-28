import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Locker = components["schemas"]["LockerResponse"];
export type LockerVisit = components["schemas"]["LockerVisitResponse"];

/**
 * The page size that holds every locker at once. The gym has 72 and nobody adds one
 * (BUSINESS_RULES.md §6), and the API's largest page is 100, so the map never pages.
 */
export const allLockersPageSize = 100;

/** How often the map polls, the same as the "currently inside" board it is drawn together with. */
export const lockersRefetchMs = 15_000;

/** Query keys. Every key starts with "lockers", like the members and plans keys. */
export const lockerKeys = {
  all: ["lockers"] as const,
  map: () => [...lockerKeys.all, "map"] as const,
  today: (id: string) => [...lockerKeys.all, "today", id] as const,
};

/** Every locker, lowest number first, refreshing on its own like the board. */
export function useAllLockers() {
  return useQuery({
    queryKey: lockerKeys.map(),
    refetchInterval: lockersRefetchMs,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/lockers", {
        params: { query: { Page: 1, PageSize: allLockersPageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data.items;
    },
  });
}

/**
 * Everyone who had the locker today, oldest first (BUSINESS_RULES.md §6 *Who had a locker today*).
 * Read when the desk asks for it, not polled: it is looked at for a moment, then closed.
 */
export function useLockerVisitsToday(id: string) {
  return useQuery({
    queryKey: lockerKeys.today(id),
    queryFn: async () => {
      const { data, error } = await api.GET("/api/lockers/{id}/today", {
        params: { path: { id } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Every locker list is refetched after any change; occupancy is also affected by check-in/out. */
function useLockerMutation<TArgs>(request: (args: TArgs) => Promise<Locker>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: lockerKeys.all });
    },
  });
}

export function useSetLockerOutOfService() {
  return useLockerMutation(async ({ id, outOfService }: { id: string; outOfService: boolean }) => {
    const options = { params: { path: { id } } };
    const { data, error } = outOfService
      ? await api.POST("/api/lockers/{id}/out-of-service", options)
      : await api.POST("/api/lockers/{id}/in-service", options);
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
