/**
 * The shop's addresses (roadmap 16.4). The router's base is `/shop` (`shopBase`), so these are what
 * follows it: the showcase is `pasargadgymplus.ir/shop/`, a product `…/shop/product/{id}`.
 */
export const shopBase = "/shop";

export const shopPaths = {
  showcase: "/",
  /** The showcase with one category chosen; the chip is a link, so it can be sent to someone. */
  category: (categoryId: string) => `/?category=${categoryId}`,
  product: (productId: string) => `/product/${productId}`,
  productPattern: "/product/:productId",
} as const;

/**
 * The gym's public page, which the shop belongs to. On the server the shop is on the same host,
 * so it is the root; in development the page is the copy `npm run dev:site` serves beside it
 * (web/vite.site.config.ts).
 */
export const gymPageAddress = import.meta.env.PROD ? "/" : "/public-site/";
