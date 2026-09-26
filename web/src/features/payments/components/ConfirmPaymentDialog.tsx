import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { formatMoney } from "@/lib/format";

import { paymentMethodLabels, type PaymentMethod } from "../api";

/** The money about to be written: what the desk is asked to have in hand (or to have handed back). */
export interface PaymentToConfirm {
  amount: string;
  method: PaymentMethod;
}

interface ConfirmPaymentDialogProps {
  /** Null keeps the box closed. */
  payment: PaymentToConfirm | null;
  /** "in" for money taken at the desk, "out" for a refund handed back. */
  direction?: "in" | "out";
  pending: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * The last question before any money is written (BUSINESS_RULES.md §5 *Confirming money at the
 * desk*). A payment is a financial record that is never deleted, and undoing a mistaken one is a
 * refund with a reason, so a stray press on "تأیید" is worth one more click that names the amount
 * and the method.
 *
 * Every form that records money opens this box from its submit, after its own validation has
 * passed, and sends the request only from `onConfirm`. "No" closes the box and leaves the form as
 * it was.
 */
export function ConfirmPaymentDialog({
  payment,
  direction = "in",
  pending,
  onConfirm,
  onCancel,
}: ConfirmPaymentDialogProps) {
  const incoming = direction === "in";

  return (
    <Dialog
      open={payment !== null}
      onOpenChange={(open) => {
        if (!open && !pending) {
          onCancel();
        }
      }}
    >
      <DialogContent>
        {payment !== null && (
          <>
            <DialogHeader>
              <DialogTitle>
                {incoming ? "آیا پول دریافت شد؟" : "آیا پول به عضو برگردانده شد؟"}
              </DialogTitle>
              <DialogDescription>
                مبلغ <strong className="text-foreground">{formatMoney(payment.amount)}</strong> با
                روش{" "}
                <strong className="text-foreground">{paymentMethodLabels[payment.method]}</strong>
                {incoming
                  ? " ثبت می‌شود. فقط اگر پول واقعاً دریافت شده، تأیید کنید."
                  : " به‌عنوان استرداد ثبت می‌شود. فقط اگر پول واقعاً به عضو برگردانده شده، تأیید کنید."}
              </DialogDescription>
            </DialogHeader>
            <div className="flex flex-wrap gap-2">
              <Button type="button" disabled={pending} onClick={onConfirm}>
                {pending
                  ? "در حال ثبت…"
                  : incoming
                    ? "بله، پول دریافت شد"
                    : "بله، پول برگردانده شد"}
              </Button>
              <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
                خیر، برگرد
              </Button>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
