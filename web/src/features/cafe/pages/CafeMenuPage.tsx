import { ArrowRight } from "lucide-react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { PageHeader } from "@/components/PageHeader";
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
    <div className="space-y-6">
      <PageHeader
        title="منوی بوفه"
        description="دسته‌بندی‌ها، محصولات و قیمت‌های بوفه."
        actions={
          <Button asChild variant="outline">
            <Link to={paths.cafe}>
              <ArrowRight aria-hidden />
              بازگشت به بوفه
            </Link>
          </Button>
        }
      />

      <div className="grid items-start gap-4 xl:grid-cols-[22rem_1fr]">
        <CategoriesCard />
        <ProductsCard />
      </div>
    </div>
  );
}
