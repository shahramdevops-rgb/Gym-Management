import { sessionStore } from "@/features/auth/session";
import { session } from "@/test/mockApi";

import { clearCacheWhenUserChanges, createQueryClient } from "./queryClient";

function jwtFor(sub: string) {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/=+$/, "");
  return `${encode({ alg: "none" })}.${encode({ sub })}.signature`;
}

describe("clearCacheWhenUserChanges", () => {
  function setUp(sub: string) {
    sessionStore.signIn(session({ accessToken: jwtFor(sub) }));
    const queryClient = createQueryClient();
    queryClient.setQueryData(["auth", "me"], { userName: sub });
    const unsubscribe = clearCacheWhenUserChanges(queryClient);

    return { queryClient, unsubscribe };
  }

  it("Cache_SessionEndsAnyWay_IsCleared", () => {
    // Not only the logout button: an expired or revoked session signs out through authFetch.
    const { queryClient, unsubscribe } = setUp("owner-id");

    sessionStore.signOut();

    expect(queryClient.getQueryData(["auth", "me"])).toBeUndefined();
    unsubscribe();
  });

  it("Cache_DifferentUserSignsIn_IsCleared", () => {
    const { queryClient, unsubscribe } = setUp("owner-id");

    sessionStore.signIn(session({ accessToken: jwtFor("staff-id") }));

    expect(queryClient.getQueryData(["auth", "me"])).toBeUndefined();
    unsubscribe();
  });

  it("Cache_SameUserGetsANewToken_IsKept", () => {
    // Every refresh issues a new token; that must not throw away everything on screen.
    const { queryClient, unsubscribe } = setUp("owner-id");

    sessionStore.signIn(
      session({ accessToken: jwtFor("owner-id"), expiresAt: "2026-09-18T11:00:00Z" }),
    );

    expect(queryClient.getQueryData(["auth", "me"])).toEqual({ userName: "owner-id" });
    unsubscribe();
  });
});
