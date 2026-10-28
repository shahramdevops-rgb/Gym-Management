import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type { components } from "@/lib/api/schema";

import { siteApi } from "../api/client";
import { siteSessionStore, useSiteSessionState } from "./session";

export type RequestCodeResponse = components["schemas"]["RequestCodeResponse"];
export type SignInResponse = components["schemas"]["SignInResponse"];

export const siteMeQueryKey = ["site", "me"] as const;

/** «دریافت کد» and «ارسال دوباره»: the API decides whether an SMS goes (BUSINESS_RULES.md §15). */
export function useRequestCode() {
  return useMutation({
    mutationFn: async (body: { phoneNumber: string }) => {
      const { data, error } = await siteApi.POST("/api/site/auth/code", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** The code: a member's or trainer's phone is signed in, a new phone gets its sign-up ticket. */
export function useSignIn() {
  return useMutation({
    mutationFn: async (body: { phoneNumber: string; code: string }) => {
      const { data, error } = await siteApi.POST("/api/site/auth/sign-in", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: (response) => {
      if (response.access !== null) {
        siteSessionStore.signIn(response.access);
      }
    },
  });
}

/** A new phone's name and birth date, with the ticket from its sign-in. */
export function useSignUp() {
  return useMutation({
    mutationFn: async (body: { ticket: string; fullName: string; birthDate: string }) => {
      const { data, error } = await siteApi.POST("/api/site/auth/sign-up", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: (access) => siteSessionStore.signIn(access),
  });
}

/**
 * Who is signed in, as the database says now: a member deactivated at the desk, or a trainer the
 * Owner stopped, is seen when the app is opened or comes back to the front (a phone app is resumed
 * far more often than reloaded), and with every new token (`SiteProviders`). The answer of the
 * sign-in itself is shown meanwhile.
 */
export function useSiteMe() {
  const state = useSiteSessionState();

  return useQuery({
    queryKey: siteMeQueryKey,
    enabled: state.status === "signedIn",
    refetchOnWindowFocus: true,
    initialData: state.status === "signedIn" ? state.access.me : undefined,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/me");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/**
 * «خروج», and the end of a stopped trainer's session. The cookie is revoked on the server; the
 * session ends here even if that request fails, since the person asked to leave. `notice` is
 * shown once on the sign-in screen.
 */
export function useSiteLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (notice: string | undefined) => {
      await siteApi.POST("/api/site/auth/logout").catch(() => undefined);
      return notice;
    },
    onSettled: (_data, _error, notice) => {
      siteSessionStore.signOut(notice);
      queryClient.clear();
    },
  });
}
