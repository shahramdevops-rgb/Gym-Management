import { Coffee, HeartPulse, ScanLine, ShoppingBag, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

/** What a visit can buy from its locker box (BUSINESS_RULES.md §7, §8). */
export type PurchaseTileKind = "cardio" | "cafe" | "shop" | "analysis";

/**
 * Each tile's look: its icon, its name, and a soft background with a strong ink of its own
 * (`--tile-*` in index.css). Whole class names, never built from pieces, so Tailwind finds them.
 */
const tiles: Record<PurchaseTileKind, { icon: LucideIcon; label: string; className: string }> = {
  cardio: {
    icon: HeartPulse,
    label: "هوازی",
    className: "bg-tile-cardio text-tile-cardio-ink border-tile-cardio-ink/25",
  },
  cafe: {
    icon: Coffee,
    label: "بوفه",
    className: "bg-tile-cafe text-tile-cafe-ink border-tile-cafe-ink/25",
  },
  shop: {
    icon: ShoppingBag,
    label: "فروشگاه",
    className: "bg-tile-shop text-tile-shop-ink border-tile-shop-ink/25",
  },
  analysis: {
    icon: ScanLine,
    label: "آنالیز",
    className: "bg-tile-analysis text-tile-analysis-ink border-tile-analysis-ink/25",
  },
};

interface PurchaseTileProps {
  kind: PurchaseTileKind;
  /**
   * What the visit already has here, shown under the name (the total and whether it is paid);
   * nothing while it has none.
   */
  summary?: ReactNode;
  /** Read out instead of the tile's text; the summary's amount belongs in it when there is one. */
  ariaLabel?: string;
  disabled?: boolean;
  onClick: () => void;
}

/**
 * One purchase slot of a visit's locker box (task 6.5.29): a coloured tile with its icon and name,
 * replacing the heading-and-button pair it used to be. The name stays on the tile once something
 * is recorded, so the amount under it never needs a heading to say what it is.
 */
export function PurchaseTile({ kind, summary, ariaLabel, disabled = false, onClick }: PurchaseTileProps) {
  const { icon: Icon, label, className } = tiles[kind];

  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      aria-label={ariaLabel}
      className={cn(
        "flex min-h-20 w-full flex-col items-center justify-center gap-1 rounded-lg border px-2 py-2 text-sm font-semibold transition hover:brightness-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 dark:hover:brightness-110",
        className,
      )}
    >
      <Icon className="size-6" aria-hidden />
      <span>{label}</span>
      {summary}
    </button>
  );
}

/** The usual summary: the total and its payment status badge, on one line. */
export function PurchaseTileSummary({ amount, badge }: { amount: string; badge: ReactNode }) {
  return (
    <span className="flex flex-wrap items-center justify-center gap-1 text-xs font-medium">
      <span>{amount}</span>
      {badge}
    </span>
  );
}
