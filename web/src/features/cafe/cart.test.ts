import { chips, proteinShake, water } from "@/test/cafe";

import { addToCart, cartTotal, parseQuantity, removeFromCart, setQuantity } from "./cart";

describe("cart", () => {
  it("addToCart_NewProduct_AddsALineOfOne", () => {
    const cart = addToCart([], water);

    expect(cart).toEqual([
      { productId: water.id, name: water.name, unitPrice: water.price, quantity: 1 },
    ]);
  });

  it("addToCart_ProductAlreadyInTheCart_RaisesItsQuantity", () => {
    // The API refuses one product on two lines (CafeOrders.DuplicateProduct).
    const cart = addToCart(addToCart([], water), water);

    expect(cart).toHaveLength(1);
    expect(cart[0]!.quantity).toBe(2);
  });

  it.each([
    [0, 1],
    [5, 5],
    [1000, 999],
  ])("setQuantity_%j_KeepsItBetweenOneAnd999_%j", (quantity, expected) => {
    const cart = setQuantity(addToCart([], water), water.id, quantity);

    expect(cart[0]!.quantity).toBe(expected);
  });

  it("removeFromCart_RemovesOnlyThatProduct", () => {
    const cart = removeFromCart(addToCart(addToCart([], water), chips), water.id);

    expect(cart.map((line) => line.productId)).toEqual([chips.id]);
  });

  it("cartTotal_SeveralLines_AddsThemUpExactly", () => {
    let cart = addToCart(addToCart([], water), proteinShake);
    cart = setQuantity(cart, water.id, 3);

    // 3 × 25,000 + 120,000
    expect(cartTotal(cart)).toBe("195000.00");
  });

  it("cartTotal_EmptyCart_IsZero", () => {
    expect(cartTotal([])).toBe("0.00");
  });

  it.each([
    ["۳", 3],
    ["12", 12],
    [" ۹۹۹ ", 999],
    ["0", null],
    ["1000", null],
    ["", null],
    ["۱.۵", null],
  ])("parseQuantity_%j_Returns%j", (text, expected) => {
    expect(parseQuantity(text)).toBe(expected);
  });
});
