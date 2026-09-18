import { sessionStore, tokenSubject, type AccessTokenResponse } from "@/features/auth/session";

/**
 * The fetch every API call goes through: it adds the access token, and when the server answers
 * 401 because the token expired, it refreshes the session once and retries the request once.
 *
 * Why here and not in each screen: the access token lives 15 minutes, so any request can be
 * the one that finds it expired. Handling that in one function means no screen ever shows
 * "please log in again" while a perfectly good refresh cookie is sitting in the browser.
 */

// Refresh calling itself on 401 would loop; login and logout never need a token.
const noRetryPaths = ["/api/auth/login", "/api/auth/refresh", "/api/auth/logout"];

/**
 * The code the API sends when the access token is missing, expired or invalid. Only this 401
 * is worth a refresh. A handler's own 401 (wrong password, locked out, deactivated) means the
 * token was fine, and refreshing would only rotate the cookie and send the request twice.
 */
const tokenRejectedCode = "Auth.Unauthenticated";
const userInactiveCode = "Auth.UserInactive";

/** The Web Locks name every tab of this app shares. */
const refreshLockName = "gym-refresh";

let refreshInFlight: Promise<boolean> | null = null;

/**
 * Asks for a new access token with the HttpOnly refresh cookie.
 *
 * Single-flight, at two levels, because rotation is strict (docs/BUSINESS_RULES.md §1) and two
 * refreshes sending the same cookie look like token theft and log the user out:
 * - Within a tab, every caller that arrives while a refresh is running waits for that one.
 * - Across tabs, the refresh runs under a Web Lock. The cookie is shared by every tab, so a tab
 *   that waited for the lock sends the cookie the first tab's refresh already rotated, which
 *   is a normal refresh and not a reuse.
 *
 * Resolves to whether the user is signed in afterwards.
 */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= withRefreshLock(requestRefresh).finally(() => {
    refreshInFlight = null;
  });

  return refreshInFlight;
}

async function withRefreshLock(refresh: () => Promise<boolean>): Promise<boolean> {
  // Every current browser has Web Locks; the fallback keeps the app working (and the test
  // runner, which has none) with the in-tab guarantee alone.
  const locks = typeof navigator === "undefined" ? undefined : navigator.locks;

  // Awaited here because the DOM typings wrap an async callback's result twice.
  return locks === undefined ? refresh() : await locks.request(refreshLockName, refresh);
}

async function requestRefresh(): Promise<boolean> {
  try {
    const response = await fetch(new URL("/api/auth/refresh", window.location.origin), {
      method: "POST",
      credentials: "same-origin",
    });

    if (!response.ok) {
      sessionStore.signOut();
      return false;
    }

    const session = (await response.json()) as AccessTokenResponse;

    // The cookie belongs to the browser, not the tab. If someone else logged in from another
    // tab, the cookie now refreshes *their* session. Taking it would silently turn this tab
    // into that user, with their rights, and the audit log would blame them for our clicks.
    const before = sessionStore.getState();
    if (
      before.status === "signedIn" &&
      tokenSubject(before.session.accessToken) !== tokenSubject(session.accessToken)
    ) {
      sessionStore.signOut();
      return false;
    }

    sessionStore.signIn(session);
    return true;
  } catch {
    // The server could not be reached. Nothing is known about the session, so it is treated
    // as ended: the login page is a safer place to wait than a screen that cannot load.
    sessionStore.signOut();
    return false;
  }
}

/**
 * Called once at startup. A reload forgets the in-memory access token, so this turns the
 * refresh cookie, if there is one, back into a session before any page decides to show login.
 */
export async function restoreSession(): Promise<void> {
  if (sessionStore.getState().status === "restoring") {
    await refreshSession();
  }
}

function withAccessToken(request: Request): Request {
  const authorized = new Request(request);
  const token = sessionStore.accessToken();

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

export async function authFetch(request: Request): Promise<Response> {
  // A clone for the first attempt, so the original body is still unread for a retry.
  const response = await fetch(withAccessToken(request.clone()));

  const path = new URL(request.url).pathname;
  if (response.status !== 401 || noRetryPaths.includes(path)) {
    return response;
  }

  const code = await problemCode(response);

  // Deactivated while signed in: there is nothing left to do in this session.
  if (code === userInactiveCode) {
    sessionStore.signOut();
    return response;
  }

  if (code !== tokenRejectedCode || !(await refreshSession())) {
    return response;
  }

  return fetch(withAccessToken(request));
}
