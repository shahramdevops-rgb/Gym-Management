import type { ReactNode } from "react";

import { Badge } from "@/components/ui/badge";
import { formatPhone } from "@/lib/format";

/**
 * A member as the trainer sees them (BUSINESS_RULES.md §15 *A member and their trainer*): name,
 * phone (a tap calls them) and one line of detail.
 */
export function Person({
  fullName,
  phoneNumber,
  detail,
  inactive = false,
}: {
  fullName: string;
  phoneNumber: string;
  detail?: ReactNode;
  inactive?: boolean;
}) {
  return (
    <div className="space-y-0.5">
      <p className="flex items-center gap-2 font-semibold">
        {fullName}
        {inactive && <Badge variant="secondary">غیرفعال</Badge>}
      </p>
      <p className="flex flex-wrap gap-x-3 text-sm text-muted-foreground">
        <a
          href={`tel:${phoneNumber}`}
          dir="ltr"
          className="text-primary underline-offset-4 hover:underline"
        >
          {formatPhone(phoneNumber)}
        </a>
        {detail !== undefined && <span>{detail}</span>}
      </p>
    </div>
  );
}
