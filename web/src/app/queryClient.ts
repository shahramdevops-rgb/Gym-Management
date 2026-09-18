import { QueryClient } from "@tanstack/react-query";

import { sessionStore, tokenSubject } from "@/features/auth/session";

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Two staff members at a front desk, not a live dashboard: refetching on every tab
        // switch would only add load and flicker.
        refetchOnWindowFocus: false,
        retry: 1,
      },
    },
  });
}

/**
 * Empties the cache whenever the person using the app changes: on every sign-out, however it
 * happened (logout button, expired or revoked session, deactivation), and if a new token
 * belongs to someone else. Otherwise the next person at the desk could see the previous
 * person's cached data, or pass a role check with their cached /me, until refetches catch up.
 *
 * Returns the unsubscribe function, for a React effect's cleanup.
 */
export function clearCacheWhenUserChanges(queryClient: QueryClient): () => void {
  const userOf = () => {
    const state = sessionStore.getState();
    return state.status === "signedIn" ? tokenSubject(state.session.accessToken) : undefined;
  };

  let previousUser = userOf();

  return sessionStore.subscribe(() => {
    const state = sessionStore.getState();
    const currentUser = userOf();

    if (state.status === "signedOut" || currentUser !== previousUser) {
      queryClient.clear();
    }

    previousUser = currentUser;
  });
}
