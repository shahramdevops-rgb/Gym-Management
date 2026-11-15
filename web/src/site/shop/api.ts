import { useQuery } from "@tanstack/react-query";
import createClient from "openapi-fetch";

import { emptyFailureAsProblem } from "@/lib/api/emptyFailureAsProblem";
import type { components, paths } from "@/lib/api/schema";

/**
 * The showcase's HTTP client (roadmap 16.4). Anyone reads the shop without signing in, so this
 * client carries no token at all: not the site's `siteAuthFetch`, and never the panel's. On the
 * gym's public address only `/api/site/shop` reaches the API (deploy/Caddyfile).
 */
const shopApi = createClient<paths>({
  baseUrl: window.location.origin,
  // The browser's fetch, looked up at each call rather than kept from when this file loaded.
  fetch: (request) => fetch(request),
});

shopApi.use(emptyFailureAsProblem);

/** The whole showcase: categories with something to show, the shown products, the contact ways. */
export type Showcase = components["schemas"]["ShowcaseResponse"];

export type ShowcaseCategory = components["schemas"]["ShowcaseCategory"];

/** One product's card: in stock or not, never how many (BUSINESS_RULES.md §16 *The showcase*). */
export type ShowcaseProductItem = components["schemas"]["ShowcaseProductItem"];

export type ShowcaseProduct = components["schemas"]["ShowcaseProductResponse"];

export type ShowcaseVariant = components["schemas"]["ShowcaseVariant"];

export type ShowcaseContacts = components["schemas"]["ShowcaseContacts"];

export const showcaseKeys = {
  all: ["showcase"] as const,
  product: (id: string) => ["showcase", "product", id] as const,
};

/**
 * A product photo's address: public, kept for a year by the browser (an id never gets other
 * bytes). `small` (480 px) for cards and thumbnails, `large` (1600 px) for the product's page.
 */
export function showcasePhotoUrl(photoId: string, size: "small" | "large"): string {
  return `/api/site/shop/photos/${photoId}/${size}`;
}

function unwrap<T>({ data, error }: { data?: T; error?: unknown }): T {
  if (error !== undefined) {
    throw error;
  }
  return data as T;
}

export function useShowcase() {
  return useQuery({
    queryKey: showcaseKeys.all,
    queryFn: async () => unwrap(await shopApi.GET("/api/site/shop/showcase")),
  });
}

/**
 * A product's id as the API writes it. An address cut short (a link copied by hand) is no product,
 * and asking the API would only bring back the answer for a route that does not exist.
 */
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isProductId(id: string): boolean {
  return guidPattern.test(id);
}

export function useShowcaseProduct(id: string) {
  return useQuery({
    queryKey: showcaseKeys.product(id),
    queryFn: async () =>
      unwrap(await shopApi.GET("/api/site/shop/products/{id}", { params: { path: { id } } })),
    enabled: isProductId(id),
    // A product that is hidden or gone stays so: asking again only delays «پیدا نشد».
    retry: false,
  });
}
