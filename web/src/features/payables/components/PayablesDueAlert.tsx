import { BellRing } from "lucide-react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { formatDate, formatMoney, formatNumber, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import { daysLeftText, kindText, payableAlertWithinDays, usePayablesDueSoon } from "../api";

const withinDays = toPersianDigits(payableAlertWithinDays);

/** Red once a payment is past its date, amber while it is only close. */
function toneOf(daysLeft: number) {
  return daysLeft < 0 ? "overdue" : "soon";
}

/** How far a payment is, as a small pill in its tone. */
function DaysLeftChip({ daysLeft }: { daysLeft: number }) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap",
        toneOf(daysLeft) === "overdue"
          ? "bg-destructive/15 text-destructive"
          : "bg-warning/15 text-warning",
      )}
    >
      {daysLeftText(daysLeft)}
    </span>
  );
}

/**
 * The Owner's alert in the header, on every page (BUSINESS_RULES.md §9 *Cheques and
 * instalments*): pending cheques and instalments dated within 5 days, and pending ones past their
 * date, which stay until they are marked on the «چک و قسط» page. Nothing at all when there are
 * none, so the header stays quiet on an ordinary day.
 *
 * The pill names the nearest one; the dialog lists every one of them. The dashboard's longer
 * 7-day list is a separate question and stays as it is.
 */
export function PayablesDueAlert() {
  const dueSoon = usePayablesDueSoon();
  const items = dueSoon.data?.items ?? [];
  const nearest = items[0];

  if (nearest === undefined) {
    return null;
  }

  // The earliest date comes first, so the first one decides whether anything is past its date.
  const tone = toneOf(nearest.daysLeft);
  const others = items.length - 1;

  return (
    <Dialog>
      <DialogTrigger
        className={cn(
          "inline-flex h-9 min-w-0 max-w-full items-center gap-2 rounded-full border px-3 text-sm",
          "shadow-xs transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
          tone === "overdue"
            ? "border-destructive/40 bg-destructive/10 hover:bg-destructive/15"
            : "border-warning/40 bg-warning/10 hover:bg-warning/15",
        )}
      >
        <span className="relative flex size-4 shrink-0 items-center justify-center">
          {tone === "overdue" && (
            <span
              aria-hidden
              className="absolute inline-flex size-full rounded-full bg-destructive/40 motion-safe:animate-ping"
            />
          )}
          <BellRing
            aria-hidden
            className={cn(
              "relative size-4",
              tone === "overdue" ? "text-destructive" : "text-warning",
            )}
          />
        </span>
        <span className="sr-only">چک و قسط نزدیک سررسید:</span>
        <span className="shrink-0 font-medium">{kindText(nearest)}</span>
        <span className="hidden min-w-0 truncate text-muted-foreground sm:inline">
          {nearest.payee}
        </span>
        <DaysLeftChip daysLeft={nearest.daysLeft} />
        {others > 0 && (
          <span
            className="shrink-0 rounded-full bg-foreground/10 px-1.5 py-0.5 text-xs font-medium"
            aria-label={`و ${formatNumber(others)} مورد دیگر`}
          >
            +{formatNumber(others)}
          </span>
        )}
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>چک و قسط نزدیک سررسید</DialogTitle>
          <DialogDescription>
            {`چک و قسطِ در انتظار تا ${withinDays} روز دیگر، و آن‌هایی که تاریخشان گذشته و هنوز پرداخت نشده‌اند.`}
          </DialogDescription>
        </DialogHeader>

        <ul className="divide-y rounded-md border">
          {items.map((item) => (
            <li
              key={item.payableId}
              className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 px-3 py-2.5 text-sm"
            >
              <div className="min-w-0 space-y-0.5">
                <p className="truncate font-medium">{item.payee}</p>
                <p className="text-xs text-muted-foreground">
                  {kindText(item)} — {formatDate(item.dueDate)}
                </p>
              </div>
              <div className="flex flex-col items-end gap-1">
                <span className="font-medium">{formatMoney(item.amount)}</span>
                <DaysLeftChip daysLeft={item.daysLeft} />
              </div>
            </li>
          ))}
        </ul>

        <DialogClose asChild>
          <Link to={paths.payables} className="text-sm font-medium hover:underline">
            همهٔ چک‌ها و قسط‌ها
          </Link>
        </DialogClose>
      </DialogContent>
    </Dialog>
  );
}
