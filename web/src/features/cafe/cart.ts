/**
 * The till's cart, as plain functions over a list of lines, so the arithmetic is tested without
 * rendering anything.
 *
 * A cart is not an order: it lives only on the till screen until "ثبت سفارش" sends it. What it
 * shows as a total is a preview, worked out exactly (no floats) from the prices the till was
 * given; the order the API returns carries the total that counts.
 */

import { addMoney, multiplyMoney } from "@/lib/money";
import { normalizeDigits } from "@/lib/normalize";

import { cafeLimits, type Product } from "./api";

export interface CartLine {
  productId: string;
  name: string;
  unitPrice: number | string;
  quantity: number;
}

/**
 * One more of a product. A product already in the cart gets a higher quantity rather than a
 * second line: the API refuses the same product twice on one order (`CafeOrders.DuplicateProduct`),
 * because a receipt that reads as two purchases of one thing is a till mistake.
 */
export function addToCart(cart: CartLine[], product: Product): CartLine[] {
  const existing = cart.find((line) => line.productId === product.id);
  if (existing === undefined) {
    return [
      ...cart,
      { productId: product.id, name: product.name, unitPrice: product.price, quantity: 1 },
    ];
  }

  return setQuantity(cart, product.id, existing.quantity + 1);
}

/** A line's quantity, kept between 1 and the most one line may hold. */
export function setQuantity(cart: CartLine[], productId: string, quantity: number): CartLine[] {
  const clamped = Math.min(Math.max(quantity, 1), cafeLimits.maxQuantity);

  return cart.map((line) => (line.productId === productId ? { ...line, quantity: clamped } : line));
}

export function removeFromCart(cart: CartLine[], productId: string): CartLine[] {
  return cart.filter((line) => line.productId !== productId);
}

export function lineTotal(line: CartLine): string {
  return multiplyMoney(line.unitPrice, line.quantity);
}

export function cartTotal(cart: CartLine[]): string {
  return addMoney(...cart.map(lineTotal));
}

/** A quantity typed with any digits, or null for anything that is not a whole number of 1 or more. */
export function parseQuantity(text: string): number | null {
  const normalized = normalizeDigits(text).trim();

  return /^[1-9]\d{0,2}$/.test(normalized) ? Number(normalized) : null;
}
