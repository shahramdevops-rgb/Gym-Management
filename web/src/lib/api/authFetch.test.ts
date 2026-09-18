import { sessionStore } from "@/features/auth/session";
import { json, mockApi, problem, session } from "@/test/mockApi";

import { authFetch, restoreSession } from "./authFetch";

const mePath = "/api/auth/me";

function request(path: string, init?: RequestInit) {
  return new Request(new URL(path, window.location.origin), init);
}

function bearer(request: Request) {
  return request.headers.get("Authorization");
}

describe("authFetch", () => {
  it("AuthFetch_SignedIn_SendsTheAccessTokenAsBearer", async () => {
    const api = mockApi({ [`GET ${mePath}`]: () => json(200, {}) });
    sessionStore.signIn(session({ accessToken: "token-1" }));

    await authFetch(request(mePath));

    expect(bearer(api.requestsTo("GET", mePath)[0]!)).toBe("Bearer token-1");
  });

  it("AuthFetch_ExpiredToken_RefreshesOnceAndRetriesWithTheNewToken", async () => {
    const api = mockApi({
      [`GET ${mePath}`]: (req) =>
        bearer(req) === "Bearer fresh"
          ? json(200, { ok: true })
          : problem(401, "Auth.Unauthenticated"),
      "POST /api/auth/refresh": () => json(200, session({ accessToken: "fresh" })),
    });
    sessionStore.signIn(session({ accessToken: "expired" }));

    const response = await authFetch(request(mePath));

    expect(response.status).toBe(200);
    expect(api.requestsTo("POST", "/api/auth/refresh")).toHaveLength(1);
    expect(api.requestsTo("GET", mePath).map(bearer)).toEqual(["Bearer expired", "Bearer fresh"]);
    expect(sessionStore.accessToken()).toBe("fresh");
  });

  it("AuthFetch_RetriedPost_SendsTheBodyAgain", async () => {
    const api = mockApi({
      "POST /api/staff": (req) =>
        bearer(req) === "Bearer fresh" ? json(201, {}) : problem(401, "Auth.Unauthenticated"),
      "POST /api/auth/refresh": () => json(200, session({ accessToken: "fresh" })),
    });
    sessionStore.signIn(session({ accessToken: "expired" }));

    await authFetch(
      request("/api/staff", { method: "POST", body: JSON.stringify({ userName: "reza" }) }),
    );

    // The first attempt read the body; the retry must still have it.
    const retried = api.requestsTo("POST", "/api/staff")[1]!;
    expect(await retried.text()).toBe('{"userName":"reza"}');
  });

  it("AuthFetch_ConcurrentUnauthorized_ShareOneRefresh", async () => {
    // Strict rotation: two refreshes with the same cookie would look like theft and log the
    // user out. Three requests failing at once must cause exactly one refresh.
    const api = mockApi({
      [`GET ${mePath}`]: (req) =>
        bearer(req) === "Bearer fresh" ? json(200, {}) : problem(401, "Auth.Unauthenticated"),
      "POST /api/auth/refresh": async () => {
        await new Promise((resolve) => setTimeout(resolve, 10));
        return json(200, session({ accessToken: "fresh" }));
      },
    });
    sessionStore.signIn(session({ accessToken: "expired" }));

    const responses = await Promise.all([
      authFetch(request(mePath)),
      authFetch(request(mePath)),
      authFetch(request(mePath)),
    ]);

    expect(responses.map((response) => response.status)).toEqual([200, 200, 200]);
    expect(api.requestsTo("POST", "/api/auth/refresh")).toHaveLength(1);
  });

  it("AuthFetch_RefreshRefused_SignsOutAndReturnsTheOriginal401", async () => {
    mockApi({
      [`GET ${mePath}`]: () => problem(401, "Auth.Unauthenticated"),
      "POST /api/auth/refresh": () => problem(401, "Auth.RefreshTokenInvalid"),
    });
    sessionStore.signIn(session());

    const response = await authFetch(request(mePath));

    expect(response.status).toBe(401);
    expect(sessionStore.getState().status).toBe("signedOut");
  });

  it("AuthFetch_LoginRefused_DoesNotTryToRefresh", async () => {
    const api = mockApi({
      "POST /api/auth/login": () => problem(401, "Auth.InvalidCredentials"),
      "POST /api/auth/refresh": () => json(200, session()),
    });
    sessionStore.signOut();

    const response = await authFetch(request("/api/auth/login", { method: "POST", body: "{}" }));

    // A wrong password is not an expired token.
    expect(response.status).toBe(401);
    expect(api.requestsTo("POST", "/api/auth/refresh")).toHaveLength(0);
  });

  it("AuthFetch_Forbidden_DoesNotRefresh", async () => {
    const api = mockApi({
      "GET /api/staff": () => problem(403, "Auth.Forbidden"),
      "POST /api/auth/refresh": () => json(200, session()),
    });
    sessionStore.signIn(session());

    const response = await authFetch(request("/api/staff"));

    expect(response.status).toBe(403);
    expect(api.requestsTo("POST", "/api/auth/refresh")).toHaveLength(0);
  });
});

describe("restoreSession", () => {
  it("RestoreSession_ValidCookie_SignsIn", async () => {
    mockApi({ "POST /api/auth/refresh": () => json(200, session({ accessToken: "restored" })) });

    await restoreSession();

    expect(sessionStore.accessToken()).toBe("restored");
  });

  it("RestoreSession_NoCookie_SignsOut", async () => {
    mockApi({ "POST /api/auth/refresh": () => problem(401, "Auth.RefreshTokenInvalid") });

    await restoreSession();

    expect(sessionStore.getState().status).toBe("signedOut");
  });

  it("RestoreSession_ServerUnreachable_SignsOut", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    await restoreSession();

    expect(sessionStore.getState().status).toBe("signedOut");
  });
});
