import createClient from "openapi-fetch";

import { emptyFailureAsProblem } from "@/lib/api/emptyFailureAsProblem";
import type { paths } from "@/lib/api/schema";

import { siteAuthFetch } from "./siteAuthFetch";

/**
 * The site app's typed HTTP client. The same generated `paths` as the panel's, since there is
 * one API, but every call goes through `siteAuthFetch` with the site's token, never the panel's.
 * The base URL is the page's own origin: Vite proxies /api in development, Caddy on the server.
 */
export const siteApi = createClient<paths>({
  baseUrl: window.location.origin,
  fetch: siteAuthFetch,
});

siteApi.use(emptyFailureAsProblem);
