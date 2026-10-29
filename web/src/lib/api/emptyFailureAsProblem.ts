import type { Middleware } from "openapi-fetch";

/**
 * A failure with no body — a 405 from an API that has no such route yet, a 502 from the proxy —
 * reaches openapi-fetch as `{ error: undefined }`, which every hook reads as "no error" and then
 * fails on the missing data with a TypeError. `errorMessage` takes a TypeError for "the server
 * could not be reached", so a server that answered was reported as unreachable (found in task 7.4,
 * when an API started before the order history existed answered 405).
 *
 * This gives such a failure a body carrying its status, so it is thrown like any other and shown
 * as the general "unexpected error" message. A file of its own, so the panel's client and the
 * site's (roadmap 15.3) both use it without one importing the other's sign-in.
 */
export const emptyFailureAsProblem: Middleware = {
  async onResponse({ response }) {
    if (response.ok || (await response.clone().text()).trim() !== "") {
      return undefined;
    }

    return new Response(JSON.stringify({ status: response.status, title: response.statusText }), {
      status: response.status,
      statusText: response.statusText,
      headers: { "Content-Type": "application/problem+json" },
    });
  },
};
