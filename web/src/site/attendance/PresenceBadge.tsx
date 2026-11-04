import { Badge } from "@/components/ui/badge";

import { cameAfterLabel, formatClock, presenceLabels, presenceVariants } from "./labels";
import type { SessionPresence } from "./api";

/**
 * A student's state in one session, with the check-in that counted: «به‌موقع ۱۳:۱۰»; an absent
 * student who came after the session is «غایب» and «بعد از سانس آمد ۱۶:۰۰» under it. May wrap,
 * so it never runs out of a narrow box.
 */
export function PresenceBadge({
  state,
  checkedInAt,
  cameAfter,
}: {
  state: SessionPresence;
  checkedInAt?: string | null;
  cameAfter: boolean;
}) {
  const time = checkedInAt === null || checkedInAt === undefined ? null : formatClock(checkedInAt);

  return (
    <span className="flex flex-col items-end gap-0.5">
      <Badge variant={presenceVariants[state]} className="max-w-full whitespace-normal">
        {presenceLabels[state]}
        {!cameAfter && time !== null && ` ${time}`}
      </Badge>
      {cameAfter && time !== null && (
        <span className="text-xs text-muted-foreground">
          {cameAfterLabel} {time}
        </span>
      )}
    </span>
  );
}
