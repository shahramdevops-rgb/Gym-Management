import { formatMoney } from "@/lib/format";

/**
 * A card's price: the one price, or «از …» with the lowest when the variants differ. The whole
 * range would not fit a phone's card; the product's page lists every variant with its own price.
 */
export function cardPrice(product: {
  minPrice: number | string;
  maxPrice: number | string;
}): string {
  const lowest = formatMoney(product.minPrice);

  return Number(product.minPrice) === Number(product.maxPrice) ? lowest : `از ${lowest}`;
}
