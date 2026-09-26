import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Plan = components["schemas"]["PlanResponse"];

export const plansPageSize = 20;

/** Query keys. Every key starts with "plans", like the members keys. */
export const planKeys = {
  all: ["plans"] as const,
  list: (filter: PlanListFilter) => [...planKeys.all, "list", filter] as const,
  detail: (id: string) => [...planKeys.all, "detail", id] as const,
  singleSession: ["plans", "single-session"] as const,
};

export interface PlanListFilter {
  /** Omitted: active and inactive plans both. */
  isActive?: boolean;
  page: number;
}

export function usePlanList(filter: PlanListFilter) {
  return useQuery({
    queryKey: planKeys.list(filter),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/plans", {
        params: {
          query: { IsActive: filter.isActive, Page: filter.page, PageSize: plansPageSize },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / plansPageSize)),
      };
    },
  });
}

/**
 * The one plan a single visit is sold from (BUSINESS_RULES.md §3), or `null` when the Owner has
 * not created it yet.
 *
 * Asked for by kind rather than found by paging: there is exactly one of them and it can sit on
 * any page of the plan list. Inactive ones are included on purpose, so the entry screen can tell
 * "there is no single-session plan" from "it is switched off" and say which — an action that
 * fails with nothing to explain it is worse than no action.
 */
export function useSingleSessionPlan() {
  return useQuery({
    queryKey: planKeys.singleSession,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/plans", {
        params: { query: { Kind: "SingleSession", Page: 1, PageSize: 1 } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data.items[0] ?? null;
    },
  });
}

export function usePlan(id: string) {
  return useQuery({
    queryKey: planKeys.detail(id),
    // A 404 will be a 404 again: asking twice only delays the "not found" message.
    retry: (failureCount, error) => !isClientError(error) && failureCount < 1,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/plans/{id}", { params: { path: { id } } });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/**
 * What the form sends. The price is a decimal string (`"1500000.50"`), never a JavaScript
 * number: the API allows prices with more digits than a number holds exactly, and the API
 * reads a JSON string into its `decimal` without passing through floating point.
 */
export interface PlanInput {
  name: string;
  durationDays: number;
  /** `null`: unlimited sessions. */
  sessionCount: number | null;
  price: string;
  /**
   * Sent only when creating the walk-in plan. Omitted means a membership, and an update never
   * sends it: a plan's kind is set once and never changes (BUSINESS_RULES.md §3).
   */
  kind?: "SingleSession";
}

/** The server's answer goes into the detail entry; every plan list is refetched. */
function usePlanMutation<TArgs>(request: (args: TArgs) => Promise<Plan>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async (plan) => {
      queryClient.setQueryData(planKeys.detail(plan.id), plan);
      await queryClient.invalidateQueries({
        queryKey: planKeys.all,
        // "single-session" as well as the lists: creating or switching off that one plan is
        // exactly what changes whether the entry screen can sell a visit at all.
        predicate: (query) =>
          query.queryKey[1] === "list" || query.queryKey[1] === "single-session",
      });
    },
  });
}

export function useCreatePlan() {
  return usePlanMutation(async (body: PlanInput) => {
    const { data, error } = await api.POST("/api/plans", { body });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useUpdatePlan() {
  return usePlanMutation(
    async ({
      id,
      version,
      name,
      durationDays,
      sessionCount,
      price,
    }: PlanInput & { id: string; version: Plan["version"] }) => {
      const { data, error } = await api.PUT("/api/plans/{id}", {
        params: { path: { id } },
        // Named field by field so `kind` is never sent: the API has no way to change it.
        // `version` is the one this form was filled from; if someone saved since, it refuses.
        body: { name, durationDays, sessionCount, price, version },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}

export function useSetPlanActive() {
  return usePlanMutation(async ({ id, active }: { id: string; active: boolean }) => {
    const options = { params: { path: { id } } };
    const { data, error } = active
      ? await api.POST("/api/plans/{id}/activate", options)
      : await api.POST("/api/plans/{id}/deactivate", options);
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
