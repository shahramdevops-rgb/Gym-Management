import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

import { sessionStore, useSessionState } from "./session";

export type CurrentUser = components["schemas"]["CurrentUserResponse"];
export type Role = "Owner" | "Staff";

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
