/**
 * One claim inside an access token. Read, not verified: the API verifies every token it
 * receives; the browser only needs to tell whether two tokens belong to the same person, or to
 * show a name. Used by the panel's session and the site's (roadmap 15.3).
 */
export function readTokenClaim(accessToken: string, claim: string): string | undefined {
  const value = readPayload(accessToken)?.[claim];
  return typeof value === "string" ? value : undefined;
}

/**
 * A claim that may appear more than once, such as `role`: the token holds a string for one value
 * and an array for several. Empty when it is missing. Read the same unverified way.
 */
export function readTokenClaims(accessToken: string, claim: string): string[] {
  const value = readPayload(accessToken)?.[claim];
  if (typeof value === "string") {
    return [value];
  }
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === "string")
    : [];
}

function readPayload(accessToken: string): Record<string, unknown> | undefined {
  try {
    const payload = accessToken.split(".")[1];
    if (payload === undefined) {
      return undefined;
    }

    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(
      atob(base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=")),
    ) as Record<string, unknown>;
  } catch {
    return undefined;
  }
}
