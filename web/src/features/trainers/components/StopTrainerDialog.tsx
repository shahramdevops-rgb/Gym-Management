import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

import type { Trainer } from "../api";

interface StopTrainerDialogProps {
  /** Null keeps the box closed. */
  trainer: Trainer | null;
  pending: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * The question before «پایان مربی‌گری» (BUSINESS_RULES.md §15 *Trainers*). It deletes nothing and
 * «شروع دوباره» undoes it, but it closes the trainer's area on the site, so a stray press is worth
 * one more click that says what happens. "No" closes the box and sends nothing.
 */
export function StopTrainerDialog({
  trainer,
  pending,
  onConfirm,
  onCancel,
}: StopTrainerDialogProps) {
  return (
    <Dialog
      open={trainer !== null}
      onOpenChange={(open) => {
        if (!open && !pending) {
          onCancel();
        }
      }}
    >
      <DialogContent>
        {trainer !== null && (
          <>
            <DialogHeader>
              <DialogTitle>پایان مربی‌گری</DialogTitle>
              <DialogDescription>
                آیا از پایان مربی‌گری{" "}
                <strong className="text-foreground">{trainer.fullName}</strong> مطمئن هستید؟ بخش
                مربی در سایت تا ۱۵ دقیقهٔ دیگر برایش بسته می‌شود. سابقهٔ او می‌ماند و هر وقت بخواهید
                با «شروع دوباره» دوباره مربی می‌شود.
              </DialogDescription>
            </DialogHeader>
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="destructive" disabled={pending} onClick={onConfirm}>
                {pending ? "در حال ثبت…" : "بله، مربی‌گری تمام شود"}
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
