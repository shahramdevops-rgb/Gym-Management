import createClient from "openapi-fetch";

import type { paths } from "./schema";

/**
 * The one typed HTTP client for the API. `paths` is generated from the API's OpenAPI document
 * (`npm run gen:api`), so a renamed route or field breaks `npm run build` rather than a screen.
 *
 * The base URL is the page's own origin: Vite proxies /api in development and Caddy does the
 * same in production, so the browser never makes a cross-origin call.
 */
export const api = createClient<paths>({ baseUrl: "/" });
