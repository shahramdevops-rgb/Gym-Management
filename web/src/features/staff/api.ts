import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type StaffMember = components["schemas"]["StaffResponse"];

export const staffPageSize = 20;

const staffQueryKey = ["staff"] as const;

export function useStaffList(page: number) {
  return useQuery({
    queryKey: [...staffQueryKey, page],
    // Keeps the current page on screen while the next one loads, instead of a blank table.
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/staff", {
        // Named as the OpenAPI document names them (the C# record's parameters). ASP.NET binds
        // query strings case-insensitively, so ?page= would work too, but the types would not.
        params: { query: { Page: page, PageSize: staffPageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / staffPageSize)),
      };
    },
  });
}

/** Every change to a staff account refreshes the list, so the table never shows stale state. */
function useStaffMutation<TArgs, TResult>(request: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: staffQueryKey }),
  });
}

export function useCreateStaff() {
  return useStaffMutation(
    async (body: { userName: string; fullName: string; temporaryPassword: string }) => {
      const { data, error } = await api.POST("/api/staff", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}

export function useSetStaffActive() {
  return useStaffMutation(async ({ id, active }: { id: string; active: boolean }) => {
    const options = { params: { path: { id } } };
    const { data, error } = active
      ? await api.POST("/api/staff/{id}/reactivate", options)
      : await api.POST("/api/staff/{id}/deactivate", options);
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useResetStaffPassword() {
  return useStaffMutation(
    async ({ id, temporaryPassword }: { id: string; temporaryPassword: string }) => {
      const { error } = await api.POST("/api/staff/{id}/reset-password", {
        params: { path: { id } },
        body: { temporaryPassword },
      });
      if (error !== undefined) {
        throw error;
      }
    },
  );
}
