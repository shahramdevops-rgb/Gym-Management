import type { AccessTokenResponse } from "@/features/auth/session";
import type { CurrentUser } from "@/features/auth/api";

type Handler = (request: Request) => Response | Promise<Response>;

/** A JSON response; failures carry the ProblemDetails content type, as the API sends them. */
export function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json" },
  });
}

/** A ProblemDetails failure with the fields the frontend reads. */
export function problem(
  status: number,
  code: string,
  extra: Record<string, unknown> = {},
): Response {
  return json(status, { status, code, correlationId: "test-correlation-id", ...extra });
}

/**
 * Stubs fetch with a tiny router keyed by "METHOD /path", so a test states exactly which API
 * calls it expects. fetch is the boundary between the app and the server, so it is the one
 * thing replaced; everything above it (openapi-fetch, authFetch, TanStack Query) runs for real.
 *
 * A request nobody expected gets a 404 and is kept in `unexpected`, which is easier to debug
 * than a hung test.
 */
export function mockApi(handlers: Record<string, Handler>) {
  const requests: Request[] = [];
  const unexpected: string[] = [];

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request =
      input instanceof Request
        ? input
        : new Request(new URL(String(input), window.location.origin), init);
    requests.push(request.clone());

    const key = `${request.method} ${new URL(request.url).pathname}`;
    const handler = handlers[key];
    if (handler === undefined) {
      unexpected.push(key);
      return problem(404, "Test.NoHandler", { detail: key });
    }

    return handler(request);
  });

  vi.stubGlobal("fetch", fetchMock);

  return {
    fetchMock,
    unexpected,
    /** The requests made to one endpoint, in order. */
    requestsTo: (method: string, path: string) =>
      requests.filter(
        (request) => request.method === method && new URL(request.url).pathname === path,
      ),
  };
}

export function session(overrides: Partial<AccessTokenResponse> = {}): AccessTokenResponse {
  return {
    accessToken: "access-token",
    expiresAt: "2026-09-18T10:15:00Z",
    mustChangePassword: false,
    ...overrides,
  };
}

export const owner: CurrentUser = {
  id: "0199a000-0000-7000-8000-000000000001",
  userName: "owner",
  fullName: "مدیر باشگاه",
  roles: ["Owner"],
  mustChangePassword: false,
};

export const staffUser: CurrentUser = {
  id: "0199a000-0000-7000-8000-000000000002",
  userName: "sara",
  fullName: "سارا رضایی",
  roles: ["Staff"],
  mustChangePassword: false,
};

/** The two calls every signed-in page makes: who am I, and is the server healthy. */
export function signedInHandlers(user: CurrentUser): Record<string, Handler> {
  return {
    "GET /api/auth/me": () => json(200, user),
    "GET /health": () => new Response("Healthy"),
  };
}
