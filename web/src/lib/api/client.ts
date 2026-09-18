import createClient from "openapi-fetch";

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
