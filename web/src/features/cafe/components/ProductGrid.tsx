import { useState } from "react";

import { Input } from "@/components/ui/input";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";

import type { Product } from "../api";

interface ProductGridProps {
  /** Sellable products only: the till never offers a ناموجود item (BUSINESS_RULES.md §8). */
  products: Product[];
  /** How many of each product are already in the cart, shown on its button. */
  inCart: ReadonlyMap<string, number>;
  onPick: (product: Product) => void;
}

/** Products in their categories, alphabetically, so the same item is always in the same place. */
function byCategory(products: Product[]): [string, Product[]][] {
  const groups = new Map<string, Product[]>();
  for (const product of products) {
    const group = groups.get(product.categoryName) ?? [];
    group.push(product);
    groups.set(product.categoryName, group);
  }

  return [...groups.entries()]
    .sort(([left], [right]) => left.localeCompare(right, "fa"))
    .map(([name, items]) => [name, items.sort((a, b) => a.name.localeCompare(b.name, "fa"))]);
}

/**
 * The till's buttons: one per product, grouped under its category, with a filter box on top for
 * a long menu. A press adds one to the cart; the cart is where a quantity is corrected.
 */
export function ProductGrid({ products, inCart, onPick }: ProductGridProps) {
  const [filter, setFilter] = useState("");
  const needle = normalizeInput(filter);
  const shown =
    needle === "" ? products : products.filter((p) => normalizeInput(p.name).includes(needle));

  return (
    <div className="space-y-4">
      <Input
        type="search"
        aria-label="جستجوی محصول"
        placeholder="جستجوی محصول…"
        value={filter}
        onChange={(event) => setFilter(event.target.value)}
      />

      {shown.length === 0 && (
        <p className="text-muted-foreground">
          {products.length === 0
            ? "هیچ محصول موجودی در منو نیست. محصول‌ها را از «منوی بوفه» اضافه یا موجود کنید."
            : "محصولی با این نام پیدا نشد."}
        </p>
      )}

      {byCategory(shown).map(([category, items]) => (
        <section key={category} aria-label={category} className="space-y-2">
          <h3 className="text-sm font-medium text-muted-foreground">{category}</h3>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-4">
            {items.map((product) => {
              const count = inCart.get(product.id);
              return (
                <button
                  key={product.id}
                  type="button"
                  className="relative flex min-h-20 flex-col items-start justify-between gap-1 rounded-md border bg-card p-3 text-start hover:bg-accent focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
                  onClick={() => onPick(product)}
                >
                  <span className="font-medium">{product.name}</span>
                  <span className="text-sm text-muted-foreground">
                    {formatMoney(product.price)}
                  </span>
                  {count !== undefined && (
                    <span className="absolute end-2 top-2 rounded-full bg-primary px-2 text-xs text-primary-foreground">
                      {toPersianDigits(count)}
                      <span className="sr-only"> عدد در سبد</span>
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        </section>
      ))}
    </div>
  );
}
