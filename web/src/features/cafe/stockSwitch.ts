import { stateEdge } from "@/lib/edges";
import { cn } from "@/lib/utils";

/**
 * The «موجود»/«ناموجود» switch on a category or a product, edged like a row's button (13.2i):
 * green when it puts something back on sale, grey when it takes it off for today.
 */
export function switchClass(isActive: boolean): string {
  return isActive
    ? cn(stateEdge.muted, "hover:bg-muted hover:text-muted-foreground")
    : cn(stateEdge.success, "hover:bg-success/10 hover:text-success");
}
