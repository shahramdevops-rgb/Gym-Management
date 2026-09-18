import { useSyncExternalStore } from "react";

import type { components } from "@/lib/api/schema";

export type AccessTokenResponse = components["schemas"]["AccessTokenResponse"];

/**
 * The login session, in memory only.
 *
 * The access token lives in this module and nowhere else: not in localStorage, not in a cookie
 * JavaScript can read (docs/BUSINESS_RULES.md §1). A script injected into the page could read
 * either of those; it cannot reach a variable inside a module. The price is that a page reload
 * forgets the token, which `restoreSession` answers by asking the API for a new one with the
 * HttpOnly refresh cookie.
 */
export type SessionState =
  | { status: "restoring" }
  | { status: "signedOut" }
  | { status: "signedIn"; session: AccessTokenResponse };

let state: SessionState = { status: "restoring" };
const listeners = new Set<() => void>();

function setState(next: SessionState) {
  state = next;
  listeners.forEach((listener) => listener());
}

export const sessionStore = {
  getState: (): SessionState => state,

  accessToken: (): string | undefined =>
    state.status === "signedIn" ? state.session.accessToken : undefined,

  signIn: (session: AccessTokenResponse) => setState({ status: "signedIn", session }),

  signOut: () => setState({ status: "signedOut" }),

  /** Back to the state before anything is known. Tests call this between cases. */
  reset: () => setState({ status: "restoring" }),

  subscribe: (listener: () => void) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};

/**
 * The user id (`sub`) inside an access token. Read, not verified: the API verifies every token
 * it receives; the browser only needs to tell whether two tokens belong to the same person.
 */
export function tokenSubject(accessToken: string): string | undefined {
  try {
    const payload = accessToken.split(".")[1];
    if (payload === undefined) {
      return undefined;
    }

    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const json = JSON.parse(
      atob(base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=")),
    ) as {
      sub?: unknown;
    };

    return typeof json.sub === "string" ? json.sub : undefined;
  } catch {
    return undefined;
  }
}

/** Re-renders the component whenever the user signs in, signs out or gets a new token. */
export function useSessionState(): SessionState {
  return useSyncExternalStore(sessionStore.subscribe, sessionStore.getState);
}
