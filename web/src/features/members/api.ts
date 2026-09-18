import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Member = components["schemas"]["MemberResponse"];

export const membersPageSize = 20;

/**
 * Query keys. Every key starts with "members", so one invalidation after any change refreshes
 * the lists, the search results and the profiles together.
 */
export const memberKeys = {
  all: ["members"] as const,
  list: (filter: MemberListFilter) => [...memberKeys.all, "list", filter] as const,
  detail: (id: string) => [...memberKeys.all, "detail", id] as const,
};

export interface MemberListFilter {
  /** Already normalized (normalizeInput). Omitted: no search. */
  search?: string;
  /** Omitted: active and inactive members both. */
  isActive?: boolean;
  page: number;
}

/** The list page and the home search: the API serves both from one endpoint. */
export function useMemberList(filter: MemberListFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: memberKeys.list(filter),
    enabled,
    // Keeps the current rows on screen while the next page or search loads, instead of
    // flashing an empty table on every key press.
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/members", {
        params: {
          query: {
            Search: filter.search,
            IsActive: filter.isActive,
            Page: filter.page,
            PageSize: membersPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / membersPageSize)),
      };
    },
  });
}

export function useMember(id: string) {
  return useQuery({
    queryKey: memberKeys.detail(id),
    // A 404 will be a 404 again: asking twice only delays the "not found" message.
    retry: (failureCount, error) => !isClientError(error) && failureCount < 1,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/members/{id}", { params: { path: { id } } });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

export interface MemberInput {
  fullName: string;
  phoneNumber: string;
  notes: string | null;
}

/**
 * After any change the server's answer goes straight into the profile's cache entry, and
 * everything else under "members" is refetched: a renamed member must not keep the old name
 * in yesterday's search results.
 */
function useMemberMutation<TArgs>(request: (args: TArgs) => Promise<Member>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async (member) => {
      queryClient.setQueryData(memberKeys.detail(member.id), member);
      await queryClient.invalidateQueries({
        queryKey: memberKeys.all,
        predicate: (query) => query.queryKey[1] === "list",
      });
    },
  });
}

export function useCreateMember() {
  return useMemberMutation(async (body: MemberInput) => {
    const { data, error } = await api.POST("/api/members", { body });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useUpdateMember() {
  return useMemberMutation(
    async ({ id, version, ...body }: MemberInput & { id: string; version: Member["version"] }) => {
      const { data, error } = await api.PUT("/api/members/{id}", {
        params: { path: { id } },
        // The version this form was filled from. If someone saved since, the API refuses.
        body: { ...body, version },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}

export function useSetMemberActive() {
  return useMemberMutation(async ({ id, active }: { id: string; active: boolean }) => {
    const options = { params: { path: { id } } };
    const { data, error } = active
      ? await api.POST("/api/members/{id}/reactivate", options)
      : await api.POST("/api/members/{id}/deactivate", options);
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

function isClientError(error: unknown): boolean {
  const status =
    typeof error === "object" && error !== null && "status" in error
      ? Number(error.status)
      : Number.NaN;

  return status >= 400 && status < 500;
}
