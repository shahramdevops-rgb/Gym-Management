import { Badge } from "@/components/ui/badge";

import { VipBadge } from "../timetable/VipBadge";
import type { Place } from "./api";

/**
 * A VIP session (15.6e), and why a student's date is not one of their fixed schedule's as it was
 * put: moved to, or its session cancelled since. A badge may wrap, so it never runs out of a narrow box.
 */
export function PlaceBadges({ place }: { place: Place }) {
  return (
    <>
      {place.isVip && <VipBadge />}
      {place.removed && (
        <Badge variant="destructive" className="max-w-full whitespace-normal">
          لغو شد
        </Badge>
      )}
      {place.moved && (
        <Badge variant="outline" className="max-w-full whitespace-normal">
          جابه‌جا شده
        </Badge>
      )}
    </>
  );
}
