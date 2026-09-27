import { KeyRound } from "lucide-react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { toPersianDigits } from "@/lib/format";

/**
 * The place a visit was given, large enough to read from across the desk: the locker's number, or
 * that a reserve place was used because every locker was full (BUSINESS_RULES.md §6).
 */
export function LockerBox({ number }: { number: number | string | null }) {
  if (number === null) {
    return <Alert role="status">ورود بدون کمد ثبت شد (همهٔ کمدها پر بود).</Alert>;
  }

  return (
    <div className="flex items-center justify-between gap-4 rounded-lg border-2 border-primary bg-primary/5 p-4">
      <div className="flex items-center gap-2 text-sm font-medium">
        <KeyRound className="size-5" aria-hidden />
        کمد شماره
      </div>
      <p
        className="text-5xl leading-none font-bold"
        aria-label={`کمد شماره ${toPersianDigits(number)}`}
      >
        {toPersianDigits(number)}
      </p>
    </div>
  );
}

export function ConfirmButtons({
  label,
  pending,
  disabled = false,
  onConfirm,
  onCancel,
}: {
  label: string;
  pending: boolean;
  disabled?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="flex flex-wrap gap-2">
      <Button disabled={pending || disabled} onClick={onConfirm}>
        {pending ? "در حال ثبت…" : label}
      </Button>
      <Button variant="outline" disabled={pending} onClick={onCancel}>
        انصراف
      </Button>
    </div>
  );
}

export function CloseButton({ onClose }: { onClose: () => void }) {
  return (
    <div className="flex">
      <Button variant="outline" onClick={onClose}>
        بستن
      </Button>
    </div>
  );
}
