import { useSyncExternalStore } from "react";

import type { components } from "@/lib/api/schema";
import { readTokenClaim } from "@/lib/jwt";

import { signUpTicketStore } from "./signUpTicket";

export type SiteAccess = components["schemas"]["SiteAccessResponse"];
export type SiteMe = components["schemas"]["SiteMeResponse"];

/**
 * The site's sign-in, in memory only, as the panel's is (`features/auth/session.ts`): the access
 * token lives in this module and nowhere a script could read it, and a reload asks the API for a
 * new one with the HttpOnly cookie. A store of its own, never the panel's (ADR 0010): the two
 * sign-ins share no state, no cookie and no token.
 *
 * `notice` is a message the sign-in screen shows once after the session ended, such as a stopped
 * trainer's «مربی‌گری شما تمام شده است» (BUSINESS_RULES.md §15 *Trainers*).
 *
 * `unreachable`: a refresh got no answer it could trust (no internet, the server restarting
 * during a release, too many requests from one address). The cookie may well be good for weeks
 * yet, and a new sign-in is a paid SMS, so the app waits for «دوباره امتحان کنید» instead of
 * signing out (BUSINESS_RULES.md §15, decided by the developer 1405/08/06).
 */
export type SiteSessionState =
  | { status: "restoring" }
  | { status: "unreachable" }
  | { status: "signedOut"; notice?: string }
  | { status: "signedIn"; access: SiteAccess };

let state: SiteSessionState = { status: "restoring" };
const listeners = new Set<() => void>();

function setState(next: SiteSessionState) {
  // A sign-up ticket belongs to the moment after its code. Once anyone signs in or out it is
  // dropped, so a phone handed on never opens someone else's sign-up form.
  if (next.status !== "restoring") {
    signUpTicketStore.clear();
  }
  state = next;
  listeners.forEach((listener) => listener());
}

export const siteSessionStore = {
  getState: (): SiteSessionState => state,

  accessToken: (): string | undefined =>
    state.status === "signedIn" ? state.access.accessToken : undefined,

  signIn: (access: SiteAccess) => setState({ status: "signedIn", access }),

  signOut: (notice?: string) =>
    setState(notice === undefined ? { status: "signedOut" } : { status: "signedOut", notice }),

  unreachable: () => setState({ status: "unreachable" }),

  /** A refresh is under way again, after «دوباره امتحان کنید». */
  restoring: () => setState({ status: "restoring" }),

  /** Back to the state before anything is known. Tests call this between cases. */
  reset: () => setState({ status: "restoring" }),

  subscribe: (listener: () => void) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};

/** The site account (`site_account`) an access token was issued for, read without verifying it. */
export function tokenSiteAccount(accessToken: string): string | undefined {
  return readTokenClaim(accessToken, "site_account");
}

/** Re-renders the component whenever the person signs in, signs out or gets a new token. */
export function useSiteSessionState(): SiteSessionState {
  return useSyncExternalStore(siteSessionStore.subscribe, siteSessionStore.getState);
}
