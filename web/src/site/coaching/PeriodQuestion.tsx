import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

/**
 * The question before «تمدید» and «ویرایش دوره» (BUSINESS_RULES.md §15 *The coaching period*: the
 * trainer is asked before each, so a slip of the finger is caught), shown once the days are typed:
 * on «برنامهٔ ثابت» and, since 15.7o, on a student's «درخواست تمدید» in «درخواست‌ها». Not the site's
 * `ConfirmDialog`: that opens from its own button and says «yes» in red, for changes that cannot be
 * taken back; this one follows a form and says «yes» in the plain colour.
 */
export function PeriodQuestion({
  title,
  question,
  confirmLabel,
  pending,
  onConfirm,
  onCancel,
}: {
  title: string;
  question: string;
  confirmLabel: string;
  pending: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onCancel()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{question}</DialogDescription>
        </DialogHeader>
        <div className="flex flex-wrap gap-2">
          <Button disabled={pending} onClick={onConfirm}>
            {pending ? "در حال ثبت…" : confirmLabel}
          </Button>
          <Button variant="outline" disabled={pending} onClick={onCancel}>
            انصراف
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
