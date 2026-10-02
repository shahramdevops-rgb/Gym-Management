import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { emptyValue } from "@/lib/format";

interface WhoCellProps {
  memberId: string | null;
  memberFullName: string | null;
  guestName: string | null;
}

/**
 * Whose row it is (BUSINESS_RULES.md §12 History): a member, linked to their profile; a guest,
 * under the name typed at the desk and marked «مهمان», with no profile to link to (§7 Guest
 * visit); or, for a cafe sale with neither, a walk-in customer.
 */
export function WhoCell({ memberId, memberFullName, guestName }: WhoCellProps) {
  if (memberId !== null) {
    return (
      <Link to={paths.member(memberId)} className="font-medium hover:underline">
        {memberFullName ?? emptyValue}
      </Link>
    );
  }

  if (guestName !== null) {
    return (
      <span className="flex items-center gap-2">
        <span className="font-medium">{guestName}</span>
        <Badge variant="outline">مهمان</Badge>
      </span>
    );
  }

  return <span className="text-muted-foreground">مشتری آزاد</span>;
}
