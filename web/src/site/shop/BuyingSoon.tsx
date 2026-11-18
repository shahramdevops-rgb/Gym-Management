import { Clock } from "lucide-react";

/**
 * The one line that says buying is not open yet (BUSINESS_RULES.md §16 *Pages the eNamad needs*,
 * decided by the developer 1405/08/27), under the shop's title and on each product's page. The
 * contact ways at the foot of the page are how a customer orders until then. Goes when 16.5/16.6
 * open buying.
 */
export function BuyingSoon() {
  return (
    <p
      role="note"
      className="flex items-start gap-2 rounded-lg border border-primary/30 bg-primary/10 px-4 py-3 text-sm leading-relaxed"
    >
      <Clock aria-hidden className="mt-0.5 size-4 shrink-0 text-primary" />
      خرید آنلاین به‌زودی باز می‌شود؛ تا آن موقع برای سفارش از راه‌های تماس پایین صفحه با ما در
      ارتباط باشید.
    </p>
  );
}
