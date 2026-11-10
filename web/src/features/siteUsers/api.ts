import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { memberKeys } from "@/features/members/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { normalizeDigits } from "@/lib/normalize";

export type SiteUser = components["schemas"]["SiteUserResponse"];

export const siteUsersPageSize = 50;

/**
 * Query keys. Under "members", so invalidating every member query (a site user registered, a plan
 * sold) refreshes this page too: a site user who becomes a member leaves it.
 */
export const siteUserKeys = {
  all: [...memberKeys.all, "siteUsers"] as const,
  list: (filter: SiteUserListFilter) => [...siteUserKeys.all, "list", filter] as const,
  byPhone: (digits: string) => [...siteUserKeys.all, "byPhone", digits] as const,
};

export interface SiteUserListFilter {
  /** Already normalized (normalizeInput). Omitted: every site user. */
  search?: string;
  page: number;
}

/** The «کاربران سایت» page (BUSINESS_RULES.md §16 *Site users*): newest first, paged. */
export function useSiteUserList(filter: SiteUserListFilter) {
  return useQuery({
    queryKey: siteUserKeys.list(filter),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/site-users", {
        params: {
          query: { Search: filter.search, Page: filter.page, PageSize: siteUsersPageSize },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / siteUsersPageSize)),
      };
    },
  });
}

/**
 * The fewest digits an Iranian mobile can be typed with (9121234567). Fewer cannot be a whole
 * number, so the form does not ask.
 */
const wholePhoneMinDigits = 10;

/** The digits of what was typed, in English, for the lookup and its cache key. */
export function phoneDigits(typed: string): string {
  return normalizeDigits(typed).replace(/\D/g, "");
}

/**
 * The new-member form's look (BUSINESS_RULES.md §16): does this whole number belong to someone who
 * signed up on the site? Only asked once the box holds enough digits for a whole number; the
 * API answers «nobody» for anything that is not one.
 */
export function useSiteUserByPhone(typed: string, { enabled = true } = {}) {
  const digits = phoneDigits(typed);

  return useQuery({
    queryKey: siteUserKeys.byPhone(digits),
    enabled: enabled && digits.length >= wholePhoneMinDigits,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/site-users/by-phone", {
        params: { query: { phoneNumber: digits } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data.siteUser;
    },
  });
}

/** «ثبت به‌عنوان عضو»: from then on a member like any other, with what they entered on the site. */
export function useRegisterSiteUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      const { data, error } = await api.POST("/api/site-users/{id}/register", {
        params: { path: { id } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: async (member) => {
      queryClient.setQueryData(memberKeys.detail(member.id), member);
      await queryClient.invalidateQueries({ queryKey: memberKeys.all });
    },
  });
}
