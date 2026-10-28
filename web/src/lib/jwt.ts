/**
 * One claim inside an access token. Read, not verified: the API verifies every token it
 * receives; the browser only needs to tell whether two tokens belong to the same person, or to
 * show a name. Used by the panel's session and the site's (roadmap 15.3).
 */
export function readTokenClaim(accessToken: string, claim: string): string | undefined {
  try {
    const payload = accessToken.split(".")[1];
    if (payload === undefined) {
      return undefined;
    }

    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const json = JSON.parse(
      atob(base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=")),
    ) as Record<string, unknown>;

    const value = json[claim];
    return typeof value === "string" ? value : undefined;
  } catch {
    return undefined;
  }
}
