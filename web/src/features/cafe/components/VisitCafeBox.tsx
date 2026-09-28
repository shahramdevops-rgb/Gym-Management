import { useState } from "react";

import { DialogSuccess } from "@/components/DialogSuccess";
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
import { formatMoney, toPersianDigits } from "@/lib/format";
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
 * The بوفه slot of one visit in its locker's box — the cafe's twin of the هوازی box
 * (BUSINESS_RULES.md §8). What the member picks up while inside goes on their account, tied to
 * this visit, and check-out lists it back to them before they leave. The till ties its own orders
 * for a member who is inside to the same visit, so both show here.
 *
 * Like the هوازی box, the slot holds only a summary and every form opens in a dialog. A purchase
 * ends on a success step that lists what was saved, from the server's answer rather than the cart,
 * so the desk can check it against what was handed over.
 */
export function VisitCafeBox({ attendanceId, member, orders }: VisitCafeBoxProps) {
  const [open, setOpen] = useState<"add" | "orders" | "done" | null>(null);
  const [saved, setSaved] = useState<CafeOrder | null>(null);
  const [announcement, setAnnouncement] = useState<string | null>(null);

  // What the payment and cancel forms in the orders list report. `aria-live` without
  // `role="status"` on purpose: the role would make every row's hidden span answer to "the page's
  // status message" and shadow the page's own; `aria-live="polite"` is what does the announcing.
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
          خرید بوفه
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
        <DialogContent className={open === "done" ? undefined : "sm:max-w-3xl"}>
          {open === "done" && saved !== null && (
            <DialogSuccess
              title="خرید بوفه ثبت شد"
              description={`به حساب ${member.fullName}`}
              onClose={() => setOpen(null)}
            >
              <SavedOrder order={saved} />
            </DialogSuccess>
          )}

          {(open === "add" || open === "orders") && (
            <DialogHeader>
              <DialogTitle>بوفه — {member.fullName}</DialogTitle>
              <DialogDescription>
                {open === "add"
                  ? "خرید به حساب عضو ثبت می‌شود و هنگام خروج به او نشان داده می‌شود."
                  : `خریدهای این مراجعه: ${formatMoney(total)}`}
              </DialogDescription>
            </DialogHeader>
          )}

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
              onDone={(order) => {
                setSaved(order);
                setOpen("done");
              }}
              onCancel={() => setOpen(orders.length === 0 ? null : "orders")}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

/** The order as the server saved it: each line with its quantity and price, and the total. */
function SavedOrder({ order }: { order: CafeOrder }) {
  return (
    <div className="space-y-2 rounded-md border p-3 text-sm">
      <ul className="space-y-1" aria-label="اقلام ثبت‌شده">
        {order.items.map((item) => (
          <li key={item.id} className="flex items-center justify-between gap-4">
            <span>
              {item.productName} × {toPersianDigits(item.quantity)}
            </span>
            <span>{formatMoney(item.lineTotal)}</span>
          </li>
        ))}
      </ul>
      <div className="flex items-center justify-between border-t pt-2 font-bold">
        <span>جمع</span>
        <span>{formatMoney(order.totalAmount)}</span>
      </div>
    </div>
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
