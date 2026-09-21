import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Locker = components["schemas"]["LockerResponse"];

export const lockersPageSize = 20;

/** Query keys. Every key starts with "lockers", like the members and plans keys. */
export const lockerKeys = {
  all: ["lockers"] as const,
  list: (page: number) => [...lockerKeys.all, "list", page] as const,
};

export function useLockerList(page: number) {
  return useQuery({
    queryKey: lockerKeys.list(page),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/lockers", {
        params: { query: { Page: page, PageSize: lockersPageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / lockersPageSize)),
      };
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

export function useCreateLocker() {
  return useLockerMutation(async (number: number) => {
    const { data, error } = await api.POST("/api/lockers", { body: { number } });
    if (error !== undefined) {
      throw error;
    }
    return data;
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
