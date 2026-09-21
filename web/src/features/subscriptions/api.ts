import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Subscription = components["schemas"]["SubscriptionResponse"];

export const subscriptionsPageSize = 10;

/** Query keys. Every key starts with "subscriptions", so one invalidation refreshes the current
 * subscription card and every history page together. */
export const subscriptionKeys = {
  all: ["subscriptions"] as const,
  memberList: (memberId: string, page: number) =>
    [...subscriptionKeys.all, "memberList", memberId, page] as const,
  current: (memberId: string) => [...subscriptionKeys.all, "current", memberId] as const,
};

async function fetchMemberSubscriptions(memberId: string, page: number, pageSize: number) {
  const { data, error } = await api.GET("/api/members/{memberId}/subscriptions", {
    params: { path: { memberId }, query: { Page: page, PageSize: pageSize } },
  });
  if (error !== undefined) {
    throw error;
  }
  return {
    items: data.items,
    totalCount: Number(data.totalCount),
    pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / pageSize)),
  };
}

/** The history tab: every subscription the member has ever had, newest first. */
export function useMemberSubscriptions(memberId: string, page: number, { enabled = true } = {}) {
  return useQuery({
    queryKey: subscriptionKeys.memberList(memberId, page),
    enabled,
    queryFn: () => fetchMemberSubscriptions(memberId, page, subscriptionsPageSize),
  });
}

/** How far back the summary card looks to find the relevant subscription (see `pickCurrent`). */
const currentSubscriptionLookback = 50;

const relevanceRank: Record<Subscription["status"], number> = {
  Active: 0,
  Frozen: 0,
  Upcoming: 1,
  Expired: 2,
  Exhausted: 2,
  Cancelled: 3,
};

/**
 * The subscription the front desk actually cares about right now, not just the newest one by
 * start date: an active (or frozen) subscription always wins, even when a later-starting queued
 * renewal exists; failing that, the soonest upcoming one; failing that, the most recently ended
 * one; a member whose whole history is cancelled falls back to the newest of those. `items` must
 * already be newest-first (by start date), what the history endpoint returns.
 */
export function pickCurrentSubscription(items: Subscription[]): Subscription | null {
  const live = items.find((item) => relevanceRank[item.status] === 0);
  if (live !== undefined) {
    return live;
  }

  const upcoming = items.filter((item) => item.status === "Upcoming");
  if (upcoming.length > 0) {
    return upcoming.reduce((soonest, item) =>
      item.startDate < soonest.startDate ? item : soonest,
    );
  }

  // Newest-first order already puts the most recently ended (or, failing that, the most recent
  // cancelled) subscription first within whatever is left.
  return items[0] ?? null;
}

/**
 * The member's most relevant subscription — exactly what the "current subscription" card shows.
 * Reuses the history endpoint with a generous page instead of a dedicated endpoint: correct for
 * any realistic member history, and the history table below remains the exact source for anyone
 * with an unusually long one.
 */
export function useCurrentSubscription(memberId: string) {
  return useQuery({
    queryKey: subscriptionKeys.current(memberId),
    queryFn: async () => {
      const page = await fetchMemberSubscriptions(memberId, 1, currentSubscriptionLookback);
      return pickCurrentSubscription(page.items);
    },
  });
}

/** Every mutation refreshes the card and every history page for this member. */
function useSubscriptionMutation<TArgs>(request: (args: TArgs) => Promise<Subscription>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: subscriptionKeys.all });
    },
  });
}

export function useAssignSubscription() {
  return useSubscriptionMutation(
    async ({ memberId, planId }: { memberId: string; planId: string }) => {
      const { data, error } = await api.POST("/api/members/{memberId}/subscriptions", {
        params: { path: { memberId } },
        body: { planId },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}

export function useRenewSubscription() {
  return useSubscriptionMutation(async ({ memberId }: { memberId: string }) => {
    const { data, error } = await api.POST("/api/members/{memberId}/subscriptions/renew", {
      params: { path: { memberId } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useFreezeSubscription() {
  return useSubscriptionMutation(async ({ id }: { id: string }) => {
    const { data, error } = await api.POST("/api/subscriptions/{id}/freeze", {
      params: { path: { id } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useUnfreezeSubscription() {
  return useSubscriptionMutation(async ({ id }: { id: string }) => {
    const { data, error } = await api.POST("/api/subscriptions/{id}/unfreeze", {
      params: { path: { id } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useCancelSubscription() {
  return useSubscriptionMutation(async ({ id, reason }: { id: string; reason: string }) => {
    const { data, error } = await api.POST("/api/subscriptions/{id}/cancel", {
      params: { path: { id } },
      body: { reason },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
