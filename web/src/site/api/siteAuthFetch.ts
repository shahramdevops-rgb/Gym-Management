import { siteSessionStore, tokenSiteAccount, type SiteAccess } from "../auth/session";

/**
 * The fetch every site call goes through: it adds the site's access token, and when the server
 * answers 401 because the token expired, refreshes the session once and retries once. The same
 * design as the panel's `lib/api/authFetch.ts`, kept apart from it (ADR 0010): its own refresh
 * address and cookie (`/api/site/auth/refresh`, `gym_site_refresh`), its own store and its own
 * lock, so nothing of one sign-in ever reaches the other.
 */

// Refresh calling itself on 401 would loop; the code, sign-in and sign-up never need a token.
const noRetryPrefix = "/api/site/auth/";

/** The API's code for a missing, expired or invalid access token; only this 401 is worth a refresh. */
const tokenRejectedCode = "Auth.Unauthenticated";

/** The Web Locks name every tab of the site app shares (the panel's is `gym-refresh`). */
const refreshLockName = "gym-site-refresh";

let refreshInFlight: Promise<boolean> | null = null;

/**
 * Asks for a new access token with the HttpOnly refresh cookie. Single-flight within a tab and,
 * through a Web Lock, across tabs: rotation is strict, and two refreshes sending the same cookie
 * look like a stolen token and end the session (BUSINESS_RULES.md §15, as §1 for the staff).
 *
 * Resolves to whether the person is signed in afterwards. Unlike the panel's, a refresh that gets
 * no trustworthy answer does not sign out: the state becomes `unreachable`.
 */
export function refreshSiteSession(): Promise<boolean> {
  refreshInFlight ??= withRefreshLock(requestRefresh).finally(() => {
    refreshInFlight = null;
  });

  return refreshInFlight;
}

async function withRefreshLock(refresh: () => Promise<boolean>): Promise<boolean> {
  const locks = typeof navigator === "undefined" ? undefined : navigator.locks;

  return locks === undefined ? refresh() : await locks.request(refreshLockName, refresh);
}

async function requestRefresh(): Promise<boolean> {
  try {
    const response = await fetch(new URL("/api/site/auth/refresh", window.location.origin), {
      method: "POST",
      credentials: "same-origin",
    });

    // Only the API's own "this session has ended" signs out (a 401: logged out, 60 days unused,
    // the phone changed, a reused cookie). Anything else says nothing about the session.
    if (response.status === 401) {
      siteSessionStore.signOut();
      return false;
    }
    if (!response.ok) {
      siteSessionStore.unreachable();
      return false;
    }

    const access = (await response.json()) as SiteAccess;

    // The cookie belongs to the browser, not the tab: if someone else signed in from another tab
    // of the same phone, the cookie now refreshes their session, and this tab must not become them.
    const before = siteSessionStore.getState();
    if (
      before.status === "signedIn" &&
      tokenSiteAccount(before.access.accessToken) !== tokenSiteAccount(access.accessToken)
    ) {
      siteSessionStore.signOut();
      return false;
    }

    siteSessionStore.signIn(access);
    return true;
  } catch {
    // The server could not be reached: nothing is known about the session, so it is kept.
    siteSessionStore.unreachable();
    return false;
  }
}

/** «دوباره امتحان کنید» after the server could not be reached. */
export async function retrySiteSession(): Promise<void> {
  siteSessionStore.restoring();
  await refreshSiteSession();
}

/** Called once at startup: turns the refresh cookie, if there is one, back into a session. */
export async function restoreSiteSession(): Promise<void> {
  if (siteSessionStore.getState().status === "restoring") {
    await refreshSiteSession();
  }
}

function withAccessToken(request: Request): Request {
  const authorized = new Request(request);
  const token = siteSessionStore.accessToken();

  if (token !== undefined) {
    authorized.headers.set("Authorization", `Bearer ${token}`);
  }

  return authorized;
}

async function problemCode(response: Response): Promise<string | undefined> {
  try {
    const body = (await response.clone().json()) as { code?: unknown };
    return typeof body.code === "string" ? body.code : undefined;
  } catch {
    return undefined;
  }
}

export async function siteAuthFetch(request: Request): Promise<Response> {
  // A clone for the first attempt, so the original body is still unread for a retry.
  const response = await fetch(withAccessToken(request.clone()));

  const path = new URL(request.url).pathname;
  if (response.status !== 401 || path.startsWith(noRetryPrefix)) {
    return response;
  }

  if ((await problemCode(response)) !== tokenRejectedCode || !(await refreshSiteSession())) {
    return response;
  }

  return fetch(withAccessToken(request));
}
