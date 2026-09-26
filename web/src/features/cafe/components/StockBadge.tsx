import { Badge } from "@/components/ui/badge";

/**
 * A product's or a category's switch as the desk reads it. The gym counts no stock, so this flag
 * is the only thing that says whether something can be bought today (BUSINESS_RULES.md §8).
 */
export function StockBadge({ inStock }: { inStock: boolean }) {
  return inStock ? (
    <Badge variant="success">موجود</Badge>
  ) : (
    <Badge variant="secondary">ناموجود</Badge>
  );
}
