import { useState } from "react";
import { Link, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { FormField, MoneyField, SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useMember } from "@/features/members/api";
import { paymentMethodLabels, paymentMethods, type PaymentMethod } from "@/features/payments/api";
import { amountProblem } from "@/features/payments/schemas";
import { errorMessage, errorMessages, fieldErrors } from "@/lib/errors";
import { formatMoney } from "@/lib/format";
import { isPositiveMoney, normalizeMoney, subtractMoney } from "@/lib/money";

import { cafeLimits, useCreateCafeOrder, useSellableProducts, type PaymentInput } from "../api";
import { addToCart, cartTotal, removeFromCart, setQuantity, type CartLine } from "../cart";
import { CafeMemberPicker } from "../components/CafeMemberPicker";
import { CartLines } from "../components/CartLines";
import { ProductGrid } from "../components/ProductGrid";

/** `65000.00` → `65000`: the total as the money box would hold it, without a fraction of nothing. */
function asTyped(amount: string): string {
  return amount.replace(/\.00$/, "");
}

/** A failed request as one Persian sentence: the field messages if there are any, else the code's. */
function problemText(problem: unknown): string {
  const perField = Object.values(fieldErrors(problem));

  return perField.length > 0 ? perField.join(" ") : errorMessage(problem);
}

/**
 * The till (BUSINESS_RULES.md §8). Products on one side, the cart and the payment on the other.
 *
 * Who is buying lives in the URL (`/cafe?member=…`), so the member profile can open the till with
 * its member already chosen. Everything else — the cart, the amount — lives only here until the
 * order is sent: a cart is not a record of anything.
 *
 * The money follows §8:
 * - a walk-in customer pays the whole total there and then, so the amount is the total and
 *   cannot be changed;
 * - a member may pay all of it, some of it, or none of it; whatever is not paid stays on their
 *   account. The amount starts at the total and follows it until somebody types in the box.
 */
export function CafeTillPage() {
  const [params, setParams] = useSearchParams();
  const memberId = params.get("member") ?? "";
  const member = useMember(memberId, { enabled: memberId !== "" });
  const products = useSellableProducts();
  const createOrder = useCreateCafeOrder();

  const [cart, setCart] = useState<CartLine[]>([]);
  // Null: the box follows the total. A string: what somebody typed, blank included.
  const [amountText, setAmountText] = useState<string | null>(null);
  const [method, setMethod] = useState<PaymentMethod>("Cash");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [amountError, setAmountError] = useState<string | undefined>(undefined);
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

  const customer = memberId === "" ? null : (member.data ?? null);
  const isWalkIn = memberId === "";
  const total = cartTotal(cart);
  const shownAmount = isWalkIn || amountText === null ? asTyped(total) : amountText;
  const inCart = new Map(cart.map((line) => [line.productId, line.quantity]));

  function chooseMember(id: string | null) {
    setParams(id === null ? {} : { member: id }, { replace: true });
    setAmountText(null);
    setAmountError(undefined);
  }

  /** The payment to send, null for "all of it on the account", or a reason to stop. */
  function paymentToSend(): { payment: PaymentInput | null } | { problem: string } {
    const amount = normalizeMoney(shownAmount).trim();
    const reference = referenceNumber.trim() === "" ? null : referenceNumber.trim();

    if (isWalkIn) {
      return { payment: { amount: total, method, referenceNumber: reference } };
    }
    // Blank or zero: nothing handed over, the whole order goes on the account.
    if (/^[0.]*$/.test(amount)) {
      return { payment: null };
    }

    const problem = amountProblem(shownAmount);
    if (problem !== null) {
      return { problem };
    }
    if (subtractMoney(total, amount).startsWith("-")) {
      return { problem: errorMessages["CafeOrders.PaidMoreThanTheOrder"]! };
    }

    return { payment: { amount, method, referenceNumber: reference } };
  }

  async function placeOrder() {
    setNotice(null);
    setAmountError(undefined);

    const decided = paymentToSend();
    if ("problem" in decided) {
      setAmountError(decided.problem);
      return;
    }

    try {
      const order = await createOrder.mutateAsync({
        memberId: isWalkIn ? null : memberId,
        items: cart.map((line) => ({ productId: line.productId, quantity: line.quantity })),
        payment: decided.payment,
      });

      const onAccount = isPositiveMoney(order.outstanding)
        ? ` ${formatMoney(order.outstanding)} به حساب ${order.memberFullName ?? "عضو"} رفت.`
        : "";
      setNotice({
        kind: "success",
        text: `سفارش ${formatMoney(order.totalAmount)} ثبت شد.${onAccount}`,
      });
      setCart([]);
      setReferenceNumber("");
      // The next customer at the counter is somebody else until the desk says otherwise.
      chooseMember(null);
    } catch (problem) {
      setNotice({ kind: "destructive", text: problemText(problem) });
      // A product switched off since the grid loaded is the likeliest reason; show today's menu.
      void products.refetch();
    }
  }

  const remaining =
    !isWalkIn && amountProblem(shownAmount) === null
      ? subtractMoney(total, normalizeMoney(shownAmount))
      : "";

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">بوفه</h2>
        <div className="flex gap-2">
          <Button asChild size="sm" variant="outline">
            <Link to={paths.cafeOrders}>سفارش‌ها</Link>
          </Button>
          <Button asChild size="sm" variant="outline">
            <Link to={paths.cafeMenu}>منوی بوفه</Link>
          </Button>
        </div>
      </div>

      {notice !== null && (
        <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
          {notice.text}
        </Alert>
      )}

      <div className="grid items-start gap-4 lg:grid-cols-[1fr_24rem]">
        <Card>
          <CardContent>
            {products.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
            {products.isError && (
              <Alert variant="destructive">{errorMessage(products.error)}</Alert>
            )}
            {products.isSuccess && (
              <ProductGrid
                products={products.data}
                inCart={inCart}
                onPick={(product) => {
                  if (!inCart.has(product.id) && cart.length >= cafeLimits.maxItems) {
                    setNotice({
                      kind: "destructive",
                      text: errorMessages["CafeOrders.TooManyItems"]!,
                    });
                    return;
                  }
                  setCart((current) => addToCart(current, product));
                }}
              />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>سبد خرید</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <CafeMemberPicker
              member={customer}
              onChange={(picked) => chooseMember(picked === null ? null : picked.id)}
            />
            {memberId !== "" && member.isPending && (
              <p className="text-sm text-muted-foreground">در حال بارگذاری عضو…</p>
            )}
            {memberId !== "" && member.isError && (
              <Alert variant="destructive">{errorMessage(member.error)}</Alert>
            )}

            <CartLines
              cart={cart}
              onQuantityChange={(productId, quantity) =>
                setCart((current) => setQuantity(current, productId, quantity))
              }
              onRemove={(productId) => setCart((current) => removeFromCart(current, productId))}
            />

            <div className="flex items-center justify-between border-t pt-3 text-lg font-bold">
              <span>جمع کل</span>
              <span>{formatMoney(total)}</span>
            </div>

            {cart.length > 0 && (
              <form
                className="space-y-3"
                noValidate
                onSubmit={(event) => {
                  event.preventDefault();
                  void placeOrder();
                }}
              >
                <MoneyField
                  label="مبلغ دریافتی (تومان)"
                  value={shownAmount}
                  disabled={isWalkIn}
                  optional={!isWalkIn}
                  error={amountError}
                  onChange={(text) => {
                    setAmountText(text);
                    setAmountError(undefined);
                  }}
                />
                {isWalkIn ? (
                  <p className="text-sm text-muted-foreground">
                    مشتری آزاد باید همین حالا کامل پرداخت کند. برای ثبت به حساب، عضو را انتخاب کنید.
                  </p>
                ) : (
                  isPositiveMoney(remaining) && (
                    <p className="text-sm">
                      {formatMoney(remaining)} به حساب {customer?.fullName ?? "عضو"} می‌رود.
                    </p>
                  )
                )}

                <SelectField
                  label="روش پرداخت"
                  value={method}
                  onChange={(event) => setMethod(event.target.value as PaymentMethod)}
                >
                  {paymentMethods.map((value) => (
                    <option key={value} value={value}>
                      {paymentMethodLabels[value]}
                    </option>
                  ))}
                </SelectField>
                <FormField
                  label="شماره پیگیری (اختیاری)"
                  dir="ltr"
                  autoComplete="off"
                  value={referenceNumber}
                  onChange={(event) => setReferenceNumber(event.target.value)}
                />

                <Button
                  type="submit"
                  className="w-full"
                  size="lg"
                  disabled={createOrder.isPending || (!isWalkIn && customer === null)}
                >
                  {createOrder.isPending ? "در حال ثبت…" : "ثبت سفارش"}
                </Button>
              </form>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
