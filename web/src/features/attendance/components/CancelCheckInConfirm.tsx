import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { isSaleKind, serviceChargeLabel } from "@/features/serviceCharges/api";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { addMoney, isPositiveMoney, subtractMoney } from "@/lib/money";

import { useEveryoneInside, type CurrentlyInside } from "../api";
import { ConfirmButtons } from "./deskParts";

type ServiceCharge = CurrentlyInside["serviceCharges"][number];
type CafeOrder = CurrentlyInside["cafeOrders"][number];

/** One thing the visit bought, as the box lists it. */
interface Purchase {
  id: string;
  label: string;
  amount: number | string;
  netPaid: number | string;
}

export interface CancelChoice {
  voidCardio: boolean;
  cafeOrderIds: string[];
  saleIds: string[];
}

interface CancelCheckInConfirmProps {
  memberFullName: string;
  /** A guest has no session to give back and no account to leave a purchase on (§7 *Guest visit*). */
  isGuest?: boolean;
  attendanceId: string;
  pending: boolean;
  onConfirm: (choice: CancelChoice) => void;
  onCancel: () => void;
}

/**
 * The questions before a check-in is cancelled (BUSINESS_RULES.md §7 *Cancel check-in*, roadmap
 * 6.5.8). A visit that bought nothing is asked once, as before. One that bought something lists
 * its هوازی, each sale (فروشگاه, آنالیز, متفرقه) and each cafe order with its own tick, all unticked: whatever is left unticked stays
 * on the member's account. When anything is ticked, a second question names it and reminds the
 * desk to hand back what was collected for it.
 *
 * A guest (§7 *Guest visit*) leaves no debt behind: a purchase left unticked must already be paid,
 * so the confirm button waits until every unpaid one is ticked or settled in the box.
 *
 * The purchases come from the "inside" list, which already carries each visit's هوازی and cafe
 * orders with what was paid, so every screen that offers cancel reads them the same way.
 */
export function CancelCheckInConfirm({
  memberFullName,
  isGuest = false,
  attendanceId,
  pending,
  onConfirm,
  onCancel,
}: CancelCheckInConfirmProps) {
  const inside = useEveryoneInside();
  const [ticked, setTicked] = useState<ReadonlySet<string>>(new Set());
  const [askingAgain, setAskingAgain] = useState(false);

  const visit = inside.data?.find((row) => row.attendanceId === attendanceId);
  const cardio = visit?.serviceCharges.find((charge) => charge.kind === "Cardio");
  const sales = (visit?.serviceCharges ?? []).filter((charge) => isSaleKind(charge.kind));
  const purchases = [
    ...(cardio === undefined ? [] : [cardioPurchase(cardio)]),
    ...sales.map(salePurchase),
    ...(visit?.cafeOrders ?? []).map(cafePurchase),
  ];
  const chosen = purchases.filter((purchase) => ticked.has(purchase.id));
  // What the server would refuse: a guest's purchase that stays standing and is not paid.
  const guestLeavesUnpaid =
    isGuest &&
    purchases.some(
      (purchase) =>
        !ticked.has(purchase.id) &&
        isPositiveMoney(subtractMoney(purchase.amount, purchase.netPaid)),
    );

  function toggle(id: string, checked: boolean) {
    setTicked((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  function send() {
    onConfirm({
      voidCardio: cardio !== undefined && ticked.has(cardio.id),
      cafeOrderIds: (visit?.cafeOrders ?? [])
        .filter((order) => ticked.has(order.id))
        .map((order) => order.id),
      saleIds: sales.filter((sale) => ticked.has(sale.id)).map((sale) => sale.id),
    });
  }

  if (askingAgain) {
    const collected = addMoney(...chosen.map((purchase) => purchase.netPaid));

    return (
      <>
        <DialogHeader>
          <DialogTitle>لغو خریدهای این مراجعه</DialogTitle>
          <DialogDescription>
            همراه ورود <strong className="text-foreground">{memberFullName}</strong> این موارد لغو
            می‌شوند:
          </DialogDescription>
        </DialogHeader>
        <ul aria-label="خریدهای لغوشونده" className="space-y-1 rounded-lg border p-3 text-sm">
          {chosen.map((purchase) => (
            <li key={purchase.id} className="flex justify-between gap-3">
              <span>{purchase.label}</span>
              <span className="font-medium">{formatMoney(purchase.amount)}</span>
            </li>
          ))}
        </ul>
        <Alert role="status">
          <p className="font-medium">
            اگر وجه این موارد را دریافت کرده‌اید، آن را به {isGuest ? "مهمان" : "عضو"} بازگردانید؛
            اگر دریافت نشده، اقدامی لازم نیست.
          </p>
          {isPositiveMoney(collected) && (
            <p className="mt-1 text-muted-foreground">
              طبق ثبت سیستم {formatMoney(collected)} دریافت شده است و بازپرداخت آن به همان روش
              پرداخت ثبت می‌شود.
            </p>
          )}
        </Alert>
        <div className="flex flex-wrap gap-2">
          <Button disabled={pending} onClick={send}>
            {pending ? "در حال ثبت…" : "بله، ورود و این موارد لغو شوند"}
          </Button>
          <Button variant="outline" disabled={pending} onClick={() => setAskingAgain(false)}>
            بازگشت
          </Button>
        </div>
      </>
    );
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>لغو ورود</DialogTitle>
        <DialogDescription>
          آیا از لغو ورود <strong className="text-foreground">{memberFullName}</strong> مطمئن هستید؟
          {isGuest ? "" : " جلسه به اشتراک او بازمی‌گردد."}
        </DialogDescription>
      </DialogHeader>

      {inside.isPending && (
        <p className="text-sm text-muted-foreground">در حال بارگذاری خریدهای این مراجعه…</p>
      )}
      {inside.isError && (
        // Sending with nothing ticked is still safe: every purchase stays on the account.
        <Alert variant="destructive">
          {errorMessage(inside.error)} خریدهای این مراجعه روی حساب عضو می‌ماند.
        </Alert>
      )}

      {purchases.length > 0 && (
        <section
          aria-label="خریدهای این مراجعه"
          className="space-y-2 rounded-lg border p-3 text-sm"
        >
          <p className="font-medium">خریدهای این مراجعه</p>
          <p className="text-muted-foreground">
            {isGuest
              ? "موارد تیک‌خورده همراه ورود لغو می‌شوند. مهمان حسابی ندارد: خرید پرداخت‌نشده‌ای که تیک نخورد، باید پیش از لغو تسویه شود."
              : "موارد تیک‌خورده همراه ورود لغو می‌شوند؛ بقیه روی حساب عضو می‌ماند."}
          </p>
          <ul className="space-y-2">
            {purchases.map((purchase) => (
              <li key={purchase.id}>
                <label className="flex cursor-pointer items-start gap-2">
                  <input
                    type="checkbox"
                    className="mt-0.5 size-4 accent-primary"
                    checked={ticked.has(purchase.id)}
                    onChange={(event) => toggle(purchase.id, event.target.checked)}
                  />
                  <span className="flex flex-1 flex-wrap justify-between gap-x-3">
                    <span>{purchase.label}</span>
                    <span className="font-medium">
                      {formatMoney(purchase.amount)}
                      {isPositiveMoney(purchase.netPaid) && (
                        <span className="font-normal text-muted-foreground">
                          {" "}
                          · پرداخت‌شده {formatMoney(purchase.netPaid)}
                        </span>
                      )}
                    </span>
                  </span>
                </label>
              </li>
            ))}
          </ul>
        </section>
      )}

      {guestLeavesUnpaid && (
        <Alert role="status">
          خرید پرداخت‌نشدهٔ مهمان را تیک بزنید تا همراه ورود لغو شود، یا ابتدا آن را تسویه کنید.
        </Alert>
      )}

      <ConfirmButtons
        label="بله، ورود لغو شود"
        pending={pending}
        disabled={inside.isPending || guestLeavesUnpaid}
        onConfirm={() => (chosen.length > 0 ? setAskingAgain(true) : send())}
        onCancel={onCancel}
      />
    </>
  );
}

function cardioPurchase(charge: ServiceCharge): Purchase {
  return { id: charge.id, label: "هوازی", amount: charge.amount, netPaid: charge.netPaid };
}

/** «فروشگاه: دستکش × ۲» for a shop item; plain «آنالیز» or «متفرقه», which have no name. */
function salePurchase(sale: ServiceCharge): Purchase {
  const quantity = Number(sale.quantity ?? 1);
  const name = sale.description;
  const detail =
    name === null || name === undefined
      ? null
      : quantity > 1
        ? `${name} × ${toPersianDigits(quantity)}`
        : name;

  return {
    id: sale.id,
    label: serviceChargeLabel(sale.kind, detail),
    amount: sale.amount,
    netPaid: sale.netPaid,
  };
}

function cafePurchase(order: CafeOrder): Purchase {
  const lines = order.items
    .map((line) => `${line.productName} × ${toPersianDigits(line.quantity)}`)
    .join("، ");

  return {
    id: order.id,
    label: `بوفه: ${lines}`,
    amount: order.totalAmount,
    netPaid: order.netPaid,
  };
}
