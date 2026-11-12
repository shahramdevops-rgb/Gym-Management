import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { readTokenClaims } from "@/lib/jwt";

import { sessionStore, useSessionState } from "./session";

export type CurrentUser = components["schemas"]["CurrentUserResponse"];
/** The panel's roles (BUSINESS_RULES.md §1); `Shop` is the shop admin, who sees only the shop (§16). */
export type Role = "Owner" | "Staff" | "Shop";

/** The gym's two roles: everything in the panel but the shop is theirs. */
export const gymRoles: readonly Role[] = ["Owner", "Staff"];

export const currentUserQueryKey = ["auth", "me"] as const;

/**
 * Who is logged in, as the database says now. Not requested while the user must change their
 * password: the API refuses /me until then, and nothing on that screen needs it.
 */
export function useCurrentUser() {
  const state = useSessionState();
  const enabled = state.status === "signedIn" && !state.session.mustChangePassword;

  return useQuery({
    queryKey: currentUserQueryKey,
    enabled,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/auth/me");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

export function hasRole(user: CurrentUser | undefined, role: Role): boolean {
  return user?.roles.includes(role) ?? false;
}

/** Whether the user holds any of the roles. */
export function hasAnyRole(user: CurrentUser | undefined, roles: readonly Role[]): boolean {
  return roles.some((role) => hasRole(user, role));
}

/**
 * The signed-in user's roles: «me»'s once it answers, and the access token's until then, so the menu
 * and the redirects need not wait for it (ADR 0011). The token is read unverified, for the screen
 * only; the API checks its signature on every request. Empty while neither says.
 */
export function useRoles(): readonly Role[] {
  const state = useSessionState();
  const user = useCurrentUser();

  if (user.data !== undefined) {
    return user.data.roles as Role[];
  }
  return state.status === "signedIn"
    ? (readTokenClaims(state.session.accessToken, "role") as Role[])
    : [];
}

/** A shop admin (ADR 0011): none of the gym's roles, so the panel shows them the shop's pages only. */
export function isShopOnly(roles: readonly Role[]): boolean {
  return roles.includes("Shop") && !roles.some((role) => gymRoles.includes(role));
}

export function useLogin() {
  return useMutation({
    mutationFn: async (body: { userName: string; password: string }) => {
      const { data, error } = await api.POST("/api/auth/login", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: (session) => sessionStore.signIn(session),
  });
}

export function useChangePassword() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: { currentPassword: string; newPassword: string }) => {
      const { data, error } = await api.POST("/api/auth/change-password", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    // The response is a fresh session whose token no longer says "must change password".
    onSuccess: async (session) => {
      sessionStore.signIn(session);
      await queryClient.invalidateQueries({ queryKey: currentUserQueryKey });
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      // Logout always answers 204. Even if the request fails, the user asked to leave, so the
      // session is ended locally either way.
      await api.POST("/api/auth/logout").catch(() => undefined);
    },
    onSettled: () => {
      sessionStore.signOut();
      // Nothing one user loaded may be shown to the next person at the desk.
      queryClient.clear();
    },
  });
}
