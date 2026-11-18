import { ImageOff } from "lucide-react";
import { useEffect } from "react";
import { Link, useSearchParams } from "react-router";

import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { errorMessage } from "@/lib/errors";
import { cn } from "@/lib/utils";

import {
  showcasePhotoUrl,
  useShowcase,
  type ShowcaseCategory,
  type ShowcaseProductItem,
} from "./api";
import { BuyingSoon } from "./BuyingSoon";
import { ContactWays } from "./ContactWays";
import { shopPaths } from "./paths";
import { cardPrice } from "./price";
import { ShopFrame } from "./ShopFrame";

export const shopTitle = "فروشگاه | باشگاه پاسارگاد پلاس";

/**
 * The shop's showcase (BUSINESS_RULES.md §16 *The showcase*): every shown product, newest first,
 * as cards with the photo, the name and the price; the categories above filter them on the page.
 * Open to anyone, without signing in. No buying yet: the contact ways at the foot are how a
 * customer orders until it opens.
 */
export function ShowcasePage() {
  const showcase = useShowcase();
  const [search] = useSearchParams();
  const categoryId = search.get("category");

  useEffect(() => {
    document.title = shopTitle;
  }, []);

  const products = showcase.data?.products ?? [];
  // A category from an old link that is no longer there shows everything, not an empty page.
  const known = showcase.data?.categories.some((category) => category.id === categoryId) ?? false;
  const shown = known ? products.filter((product) => product.categoryId === categoryId) : products;

  return (
    <ShopFrame>
      <div className="space-y-2">
        <p className="text-sm font-semibold text-primary">فروشگاه</p>
        <h1 className="text-2xl leading-snug font-extrabold sm:text-4xl sm:leading-snug">
          هرچه برای تمرین لازم داری، همین‌جا.
        </h1>
      </div>

      <BuyingSoon />

      {showcase.isPending && <LoadingState />}
      {showcase.isError && <Alert variant="destructive">{errorMessage(showcase.error)}</Alert>}

      {showcase.isSuccess && (
        <>
          {products.length === 0 ? (
            <EmptyState>هنوز کالایی در فروشگاه نیست.</EmptyState>
          ) : (
            <div className="space-y-5">
              {showcase.data.categories.length > 1 && (
                <CategoryChips
                  categories={showcase.data.categories}
                  selected={known ? categoryId : null}
                />
              )}

              <ul
                className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4"
                aria-label="کالاهای فروشگاه"
              >
                {shown.map((product) => (
                  <li key={product.id}>
                    <ProductCard product={product} />
                  </li>
                ))}
              </ul>
            </div>
          )}

          <ContactWays contacts={showcase.data.contacts} />
        </>
      )}
    </ShopFrame>
  );
}

/** «همه» and one chip per category. Links, so a category's page can be sent to someone. */
function CategoryChips({
  categories,
  selected,
}: {
  categories: ShowcaseCategory[];
  selected: string | null;
}) {
  const chip = (active: boolean) =>
    cn(
      "rounded-full border px-3.5 py-1.5 text-sm transition-colors",
      active
        ? "border-primary bg-primary font-semibold text-primary-foreground"
        : "text-muted-foreground hover:border-primary hover:text-foreground",
    );

  return (
    <nav aria-label="دسته‌بندی" className="flex flex-wrap gap-2">
      <Link
        to={shopPaths.showcase}
        className={chip(selected === null)}
        aria-current={selected === null ? "page" : undefined}
      >
        همه
      </Link>
      {categories.map((category) => (
        <Link
          key={category.id}
          to={shopPaths.category(category.id)}
          className={chip(selected === category.id)}
          aria-current={selected === category.id ? "page" : undefined}
        >
          {category.name}
        </Link>
      ))}
    </nav>
  );
}

/**
 * One product: its main photo whole on a square (`object-contain`, never cut), then the name and
 * the price. A product with nothing in stock is still shown, marked «ناموجود» (§16).
 */
function ProductCard({ product }: { product: ShowcaseProductItem }) {
  return (
    <Link
      to={shopPaths.product(product.id)}
      className="group flex h-full flex-col overflow-hidden rounded-md border bg-card transition-colors hover:border-primary focus-visible:outline-2 focus-visible:outline-ring"
    >
      <div className="relative grid aspect-square place-items-center bg-muted/40">
        {product.mainPhotoId === null ? (
          <ImageOff aria-hidden className="size-8 text-muted-foreground" />
        ) : (
          <img
            src={showcasePhotoUrl(product.mainPhotoId, "small")}
            alt=""
            loading="lazy"
            className={cn("size-full object-contain", !product.inStock && "opacity-60")}
          />
        )}
        {!product.inStock && (
          <span className="absolute start-2 top-2 rounded-sm bg-background/85 px-2 py-0.5 text-xs font-semibold text-muted-foreground">
            ناموجود
          </span>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-1 p-3">
        <h2 className="text-sm leading-relaxed font-semibold break-words sm:text-base">
          {product.name}
        </h2>
        <p
          className={cn(
            "mt-auto text-sm font-semibold",
            product.inStock ? "text-primary" : "text-muted-foreground",
          )}
        >
          {cardPrice(product)}
        </p>
      </div>
    </Link>
  );
}
