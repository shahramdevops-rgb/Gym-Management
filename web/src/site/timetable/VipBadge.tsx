import { Badge } from "@/components/ui/badge";

/**
 * «VIP» beside a session the trainer marked so (BUSINESS_RULES.md §15 *VIP sessions*, 15.6e): for
 * now only a label, seen by the trainer and the student alike. In English, as the developer chose.
 */
export function VipBadge() {
  return (
    <Badge variant="outline" className="border-warning px-1.5 text-warning-ink">
      VIP
    </Badge>
  );
}
