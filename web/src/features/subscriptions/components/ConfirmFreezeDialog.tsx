import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { formatNumber } from "@/lib/format";

export type FreezeAction = "freeze" | "unfreeze";

/**
 * `Gym:MaxFreezeDaysPerSubscription` (BUSINESS_RULES.md §0). The API enforces it; this copy only
 * tells the Owner about it, so it must be changed together with the server setting.
 */
export const maxFreezeDays = 30;

interface ConfirmFreezeDialogProps {
  /** Null keeps the box closed. */
  action: FreezeAction | null;
  /** Which subscription, as the row names it (plan and start date). */
  context: string;
  /** Days used by earlier freezes; a freeze still running is not counted until it ends. */
  totalFrozenDays: number;
  pending: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * The question before a subscription is frozen or unfrozen (BUSINESS_RULES.md §4 *Freeze*). Both
 * are one click on the row and both change the member's dates — freeze days are counted from
 * today, and unfreezing moves the end date and any queued renewal — so a stray press is worth one
 * more click that names the subscription and how many freeze days it has left. "No" closes the
 * box and sends nothing.
 */
export function ConfirmFreezeDialog({
  action,
  context,
  totalFrozenDays,
  pending,
  onConfirm,
  onCancel,
}: ConfirmFreezeDialogProps) {
  const freezing = action === "freeze";
  const daysLeft = Math.max(0, maxFreezeDays - totalFrozenDays);

  return (
    <Dialog
      open={action !== null}
      onOpenChange={(open) => {
        if (!open && !pending) {
          onCancel();
        }
      }}
    >
      <DialogContent>
        {action !== null && (
          <>
            <DialogHeader>
              <DialogTitle>{freezing ? "فریز اشتراک" : "رفع فریز اشتراک"}</DialogTitle>
              <DialogDescription>
                {freezing ? "آیا از فریز " : "آیا از رفع فریز "}
                <strong className="text-foreground">{context}</strong>
                {freezing
                  ? " مطمئن هستید؟ تا رفع فریز، از این اشتراک جلسه‌ای استفاده نمی‌شود و روزهای فریز از امروز شمرده می‌شود."
                  : " مطمئن هستید؟ روزهای فریز به تاریخ پایان اضافه می‌شود و اشتراک از امروز دوباره فعال است."}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-1 rounded-lg border bg-muted/40 p-3 text-sm">
              <p className="font-medium">
                هر اشتراک حداکثر {formatNumber(maxFreezeDays)} روز فریز دارد.
              </p>
              <p className="text-muted-foreground">
                تاکنون {formatNumber(Math.min(totalFrozenDays, maxFreezeDays))} روز استفاده شده و{" "}
                {formatNumber(daysLeft)} روز باقی مانده است.{" "}
                {freezing
                  ? `اگر فریز بیشتر از ${formatNumber(daysLeft)} روز طول بکشد، فقط ${formatNumber(daysLeft)} روز به تاریخ پایان اضافه می‌شود.`
                  : `از این فریز حداکثر ${formatNumber(daysLeft)} روز به تاریخ پایان اضافه می‌شود.`}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button type="button" disabled={pending} onClick={onConfirm}>
                {pending ? "در حال ثبت…" : freezing ? "بله، فریز شود" : "بله، فریز برداشته شود"}
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
