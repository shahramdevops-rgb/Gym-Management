import { sessionStore, type AccessTokenResponse } from "@/features/auth/session";

/**
 * The fetch every API call goes through: it adds the access token, and when the server answers
 * 401 it refreshes the session once and retries the request once.
 *
 * Why here and not in each screen: the access token lives 15 minutes, so any request can be
 * the one that finds it expired. Handling that in one function means no screen ever shows
 * "please log in again" while a perfectly good refresh cookie is sitting in the browser.
 */

// These answer 401 for reasons a refresh cannot fix (wrong password, no cookie), and refresh
// calling itself on 401 would loop.
const noRetryPaths = ["/api/auth/login", "/api/auth/refresh", "/api/auth/logout"];

let refreshInFlight: Promise<boolean> | null = null;

/**
 * Asks for a new access token with the HttpOnly refresh cookie.
 *
 * Single-flight: every caller that arrives while a refresh is running waits for that same
 * refresh. Rotation is strict (docs/BUSINESS_RULES.md §1), so two refreshes sending the same
 * cookie would look like token theft and log the user out; this is the frontend's half of
 * that rule. Resolves to whether the user is signed in afterwards.
 */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= requestRefresh().finally(() => {
    refreshInFlight = null;
  });

  return refreshInFlight;
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

    sessionStore.signIn((await response.json()) as AccessTokenResponse);
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

export async function authFetch(request: Request): Promise<Response> {
  // A clone for the first attempt, so the original body is still unread for a retry.
  const response = await fetch(withAccessToken(request.clone()));

  const path = new URL(request.url).pathname;
  if (response.status !== 401 || noRetryPaths.includes(path)) {
    return response;
  }

  if (!(await refreshSession())) {
    return response;
  }

  return fetch(withAccessToken(request));
}
