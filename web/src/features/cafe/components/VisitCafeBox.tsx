import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { PaymentStatusBadge } from "@/features/payments/components/PaymentStatusBadge";
import { errorMessage } from "@/lib/errors";
import { formatMoney } from "@/lib/format";
import { addMoney, isPositiveMoney } from "@/lib/money";

import { cafeLimits, useCreateCafeOrder, useSellableProducts, type CafeOrder } from "../api";
import { addToCart, cartTotal, removeFromCart, setQuantity, type CartLine } from "../cart";
import { CafeOrdersTable } from "./CafeOrdersTable";
import { CartLines } from "./CartLines";
import { ProductGrid } from "./ProductGrid";

interface VisitCafeBoxProps {
  attendanceId: string;
  member: { id: string; fullName: string };
  /** The visit's standing orders, as the board already carries them. */
  orders: CafeOrder[];
}

/**
 * The بوفه slot of one visit on the "currently inside" board — the cafe's twin of the هوازی box
 * (BUSINESS_RULES.md §8). What the member picks up while inside goes on their account, tied to
 * this visit, and check-out lists it back to them before they leave.
 *
 * Like the هوازی box, the cell holds only a summary and every form opens in a dialog, so the row
 * stays one line (task 6.5.2).
 */
export function VisitCafeBox({ attendanceId, member, orders }: VisitCafeBoxProps) {
  const [open, setOpen] = useState<"add" | "orders" | null>(null);
  const [announcement, setAnnouncement] = useState<string | null>(null);

  function done(text: string) {
    setOpen(null);
    setAnnouncement(text);
  }

  // `aria-live` without `role="status"`, for the reason given on ServiceChargeBox.
  const announcer = (
    <span aria-live="polite" aria-atomic="true" className="sr-only">
      {announcement}
    </span>
  );

  const total = addMoney(...orders.map((order) => order.totalAmount));
  const netPaid = addMoney(...orders.map((order) => order.netPaid));
  const outstanding = addMoney(...orders.map((order) => order.outstanding));
  const status = !isPositiveMoney(outstanding)
    ? "Paid"
    : isPositiveMoney(netPaid)
      ? "Partial"
      : "Unpaid";

  return (
    <>
      {announcer}

      {orders.length === 0 ? (
        <Button size="sm" variant="outline" onClick={() => setOpen("add")}>
          افزودن خرید بوفه
        </Button>
      ) : (
        <button
          type="button"
          onClick={() => setOpen("orders")}
          aria-label={`بوفه: ${formatMoney(total)}`}
          className="flex items-center gap-2 rounded-md px-1 py-0.5 text-sm hover:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <span>{formatMoney(total)}</span>
          <PaymentStatusBadge status={status} />
        </button>
      )}

      <Dialog open={open !== null} onOpenChange={(next) => !next && setOpen(null)}>
        <DialogContent className="sm:max-w-3xl">
          <DialogHeader>
            <DialogTitle>بوفه — {member.fullName}</DialogTitle>
            <DialogDescription>
              {open === "add"
                ? "خرید به حساب عضو ثبت می‌شود و هنگام خروج به او نشان داده می‌شود."
                : `خریدهای این مراجعه: ${formatMoney(total)}`}
            </DialogDescription>
          </DialogHeader>

          {open === "orders" && (
            <div className="space-y-3">
              <CafeOrdersTable orders={orders} showCustomer={false} onDone={setAnnouncement} />
              <Button size="sm" onClick={() => setOpen("add")}>
                خرید دیگر
              </Button>
            </div>
          )}

          {open === "add" && (
            <VisitPurchaseForm
              attendanceId={attendanceId}
              memberId={member.id}
              onDone={(order) =>
                done(`${formatMoney(order.totalAmount)} به حساب ${member.fullName} ثبت شد.`)
              }
              onCancel={() => setOpen(orders.length === 0 ? null : "orders")}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

interface VisitPurchaseFormProps {
  attendanceId: string;
  memberId: string;
  onDone: (order: CafeOrder) => void;
  onCancel: () => void;
}

/**
 * The till's grid and cart, for one member inside the gym. Nothing is paid here: the order goes
 * on the account (`payment: null`), and the money is taken at check-out or whenever the member
 * settles, like a هوازی charge.
 */
function VisitPurchaseForm({ attendanceId, memberId, onDone, onCancel }: VisitPurchaseFormProps) {
  const products = useSellableProducts();
  const createOrder = useCreateCafeOrder();
  const [cart, setCart] = useState<CartLine[]>([]);
  const [error, setError] = useState<string | null>(null);

  const inCart = new Map(cart.map((line) => [line.productId, line.quantity]));

  async function submit() {
    setError(null);
    try {
      const order = await createOrder.mutateAsync({
        memberId,
        attendanceId,
        items: cart.map((line) => ({ productId: line.productId, quantity: line.quantity })),
        payment: null,
      });
      onDone(order);
    } catch (problem) {
      setError(errorMessage(problem));
      void products.refetch();
    }
  }

  return (
    <div className="grid gap-4 md:grid-cols-[1fr_16rem]">
      <div>
        {products.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
        {products.isError && <Alert variant="destructive">{errorMessage(products.error)}</Alert>}
        {products.isSuccess && (
          <ProductGrid
            products={products.data}
            inCart={inCart}
            onPick={(product) => {
              if (!inCart.has(product.id) && cart.length >= cafeLimits.maxItems) {
                return;
              }
              setCart((current) => addToCart(current, product));
            }}
          />
        )}
      </div>

      <div className="space-y-3">
        <CartLines
          cart={cart}
          onQuantityChange={(productId, quantity) =>
            setCart((current) => setQuantity(current, productId, quantity))
          }
          onRemove={(productId) => setCart((current) => removeFromCart(current, productId))}
        />
        <div className="flex items-center justify-between border-t pt-3 font-bold">
          <span>جمع</span>
          <span>{formatMoney(cartTotal(cart))}</span>
        </div>
        {error !== null && <Alert variant="destructive">{error}</Alert>}
        <div className="flex gap-2">
          <Button
            disabled={cart.length === 0 || createOrder.isPending}
            onClick={() => void submit()}
          >
            {createOrder.isPending ? "در حال ثبت…" : "ثبت به حساب عضو"}
          </Button>
          <Button variant="ghost" onClick={onCancel}>
            انصراف
          </Button>
        </div>
      </div>
    </div>
  );
}
