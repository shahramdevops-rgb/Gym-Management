import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Button } from "@/components/ui/button";

import { CategoriesCard } from "../components/CategoriesCard";
import { ProductsCard } from "../components/ProductsCard";

/**
 * The cafe's price list, categories and products on one page: the category list is short, and
 * a new shelf is usually added together with its first products. Both roles use all of it —
 * the Owner decided staff have no restriction in the cafe (BUSINESS_RULES.md §8).
 */
export function CafeMenuPage() {
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">منوی بوفه</h2>
        <Button asChild size="sm">
          <Link to={paths.cafe}>بازگشت به بوفه</Link>
        </Button>
      </div>

      <div className="grid items-start gap-4 xl:grid-cols-[22rem_1fr]">
        <CategoriesCard />
        <ProductsCard />
      </div>
    </div>
  );
}
