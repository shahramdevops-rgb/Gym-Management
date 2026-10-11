import { Badge } from "@/components/ui/badge";
import { stateEdge } from "@/lib/edges";
import { cn } from "@/lib/utils";

/**
 * A product's or a category's switch as the desk reads it. The gym counts no stock, so this flag
 * is the only thing that says whether something can be bought today (BUSINESS_RULES.md §8).
 * Edged like the history's tags (13.2i): green for on sale, grey for not today.
 */
export function StockBadge({ inStock }: { inStock: boolean }) {
  return inStock ? (
    <Badge variant="ghost" className={cn(stateEdge.success, "font-semibold")}>
      موجود
    </Badge>
  ) : (
    <Badge variant="ghost" className={stateEdge.muted}>
      ناموجود
    </Badge>
  );
}
