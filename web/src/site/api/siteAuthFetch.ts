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
 * Resolves to whether the person is signed in afterwards.
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

    if (!response.ok) {
      siteSessionStore.signOut();
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
    // The server could not be reached: the sign-in screen is a safer place to wait.
    siteSessionStore.signOut();
    return false;
  }
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
