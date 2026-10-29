import { useState, type ReactNode } from "react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

interface ConfirmDialogProps {
  /** The button that opens the question; also the question's title. */
  label: string;
  /** What happens, said in full: «… از امروز تمام می‌شود. ادامه می‌دهید؟» */
  question: ReactNode;
  pending: boolean;
  /** Called once the person says yes; the box closes when the change is done or refused. */
  onConfirm: (close: () => void) => void;
}

/**
 * A button that asks before a change that cannot be taken back (BUSINESS_RULES.md §15 *A member
 * and their trainer*: ending a link «asks first»). A short question stays a card on a phone, as
 * the panel's confirmations do (13.1c). The site has its own, since the panel's sits among the
 * front desk's parts, which the site never imports (ADR 0010).
 */
export function ConfirmDialog({ label, question, pending, onConfirm }: ConfirmDialogProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
        {label}
      </Button>
      <Dialog open={open} onOpenChange={(next) => !pending && setOpen(next)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{label}</DialogTitle>
            <DialogDescription>{question}</DialogDescription>
          </DialogHeader>
          <div className="flex flex-wrap gap-2">
            <Button
              variant="destructive"
              disabled={pending}
              onClick={() => onConfirm(() => setOpen(false))}
            >
              {pending ? "در حال ثبت…" : label}
            </Button>
            <Button variant="outline" disabled={pending} onClick={() => setOpen(false)}>
              انصراف
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
