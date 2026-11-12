import { Badge } from "@/components/ui/badge";
import { gymToday } from "@/lib/format";
import { cn } from "@/lib/utils";

/**
 * «جدید» beside something the last release added, so the Owner and Staff notice it without being
 * told (asked by the developer 2026-11-12). It shows until `until`, an ISO date in the gym's time
 * zone, the first day it is gone: one week after the release, with no click needed. Panel only for
 * now; the site app has none.
 *
 * Hidden from screen readers: it is decoration, and a link's name stays what it always was.
 */
export function NewBadge({ until, className }: { until: string; className?: string }) {
  if (!isNew(until)) {
    return null;
  }

  return (
    <Badge aria-hidden className={cn("px-1.5 py-0 text-[0.625rem] leading-4", className)}>
      جدید
    </Badge>
  );
}

/** True while today, in the gym's time zone, is before `until` (ISO dates compare as text). */
function isNew(until: string): boolean {
  return gymToday() < until;
}
