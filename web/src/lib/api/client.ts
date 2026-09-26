import createClient, { type Middleware } from "openapi-fetch";

import { authFetch } from "./authFetch";
import type { paths } from "./schema";

/**
 * The one typed HTTP client for the API. `paths` is generated from the API's OpenAPI document
 * (`npm run gen:api`), so a renamed route or field breaks `npm run build` rather than a screen.
 *
 * The base URL is the page's own origin: Vite proxies /api in development and Caddy does the
 * same in production, so the browser never makes a cross-origin call. It is absolute rather
 * than "/" because the Request constructor outside a browser (the test runner) rejects a
 * relative URL.
 *
 * Every call goes through authFetch, which adds the access token and refreshes it on 401.
 */
export const api = createClient<paths>({
  baseUrl: window.location.origin,
  fetch: authFetch,
});

/**
 * A failure with no body — a 405 from an API that has no such route yet, a 502 from the proxy —
 * reaches openapi-fetch as `{ error: undefined }`, which every hook reads as "no error" and then
 * fails on the missing data with a TypeError. `errorMessage` takes a TypeError for "the server
 * could not be reached", so a server that answered was reported as unreachable (found in task 7.4,
 * when an API started before the order history existed answered 405).
 *
 * This gives such a failure a body carrying its status, so it is thrown like any other and shown
 * as the general "unexpected error" message.
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

api.use(emptyFailureAsProblem);
