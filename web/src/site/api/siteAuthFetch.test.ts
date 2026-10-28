import { sessionStore } from "@/features/auth/session";
import { json, mockApi, problem } from "@/test/mockApi";

import { siteSessionStore } from "../auth/session";
import { siteAccess } from "../test/renderSite";
import { siteApi } from "./client";
import { restoreSiteSession } from "./siteAuthFetch";

describe("siteAuthFetch", () => {
  it("SiteCall_SignedInToBoth_SendsTheSitesTokenOnly", async () => {
    sessionStore.signIn({
      accessToken: "staff-token",
      expiresAt: "2026-10-28T10:15:00Z",
      mustChangePassword: false,
    });
    siteSessionStore.signIn(siteAccess());
    const api = mockApi({ "GET /api/site/me": () => json(200, siteAccess().me) });

    await siteApi.GET("/api/site/me");

    expect(api.requestsTo("GET", "/api/site/me")[0]!.headers.get("Authorization")).toBe(
      "Bearer site-access-token",
    );
  });

  it("SiteCall_ExpiredToken_RefreshesWithTheSitesCookieAndRetriesOnce", async () => {
    siteSessionStore.signIn(siteAccess());
    let meCalls = 0;
    const api = mockApi({
      "GET /api/site/me": () =>
        ++meCalls === 1 ? problem(401, "Auth.Unauthenticated") : json(200, siteAccess().me),
      "POST /api/site/auth/refresh": () =>
        json(200, { ...siteAccess(), accessToken: "site-access-token" }),
    });

    const { data } = await siteApi.GET("/api/site/me");

    expect(data?.member?.fullName).toBe("علی محمدی");
    expect(api.requestsTo("POST", "/api/site/auth/refresh")).toHaveLength(1);
    expect(api.requestsTo("POST", "/api/auth/refresh")).toHaveLength(0);
    expect(meCalls).toBe(2);
  });

  it("SiteCall_RefreshRefused_SignsOutOfTheSiteOnly", async () => {
    sessionStore.signIn({
      accessToken: "staff-token",
      expiresAt: "2026-10-28T10:15:00Z",
      mustChangePassword: false,
    });
    siteSessionStore.signIn(siteAccess());
    mockApi({
      "GET /api/site/me": () => problem(401, "Auth.Unauthenticated"),
      "POST /api/site/auth/refresh": () => problem(401, "SiteAuth.SessionInvalid"),
    });

    await siteApi.GET("/api/site/me");

    expect(siteSessionStore.getState().status).toBe("signedOut");
    expect(sessionStore.getState().status).toBe("signedIn");
  });

  it("Reload_WithTheSitesCookie_RestoresTheSession", async () => {
    const api = mockApi({ "POST /api/site/auth/refresh": () => json(200, siteAccess()) });

    await restoreSiteSession();

    expect(siteSessionStore.accessToken()).toBe("site-access-token");
    expect(api.requestsTo("POST", "/api/site/auth/refresh")).toHaveLength(1);
  });

  it("SiteCall_TwoExpiredAtOnce_RefreshOnce", async () => {
    siteSessionStore.signIn(siteAccess());
    let refreshed = false;
    const api = mockApi({
      "GET /api/site/me": () =>
        refreshed ? json(200, siteAccess().me) : problem(401, "Auth.Unauthenticated"),
      "POST /api/site/auth/refresh": () => {
        refreshed = true;
        return json(200, siteAccess());
      },
    });

    await Promise.all([siteApi.GET("/api/site/me"), siteApi.GET("/api/site/me")]);

    expect(api.requestsTo("POST", "/api/site/auth/refresh")).toHaveLength(1);
  });

  it("Refresh_CookieNowSomeoneElses_SignsOutRatherThanBecomingThem", async () => {
    siteSessionStore.signIn({ ...siteAccess(), accessToken: tokenFor("account-a") });
    mockApi({
      "GET /api/site/me": () => problem(401, "Auth.Unauthenticated"),
      "POST /api/site/auth/refresh": () =>
        json(200, { ...siteAccess(), accessToken: tokenFor("account-b") }),
    });

    await siteApi.GET("/api/site/me");

    expect(siteSessionStore.getState().status).toBe("signedOut");
  });
});

/** An unsigned token whose payload names the site account; the browser never verifies it. */
function tokenFor(siteAccount: string): string {
  const payload = btoa(JSON.stringify({ site_account: siteAccount }))
    .replace(/=+$/, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
  return `header.${payload}.signature`;
}
