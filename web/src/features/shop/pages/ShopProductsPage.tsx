import { ImageOff, Plus } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";

import { PageHeader } from "@/components/PageHeader";
import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { paths } from "@/app/paths";
import { useRoles } from "@/features/auth/api";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import {
  photoUrl,
  useShopCategories,
  useShopProducts,
  type ShopCategory,
  type ShopProductListItem,
} from "../api";

/**
 * «کالاها», the shop admin's home (BUSINESS_RULES.md §16 *Products*): every product, hidden ones
 * too, newest first, as cards with the main photo, the price (or its range across variants) and the
 * stock. The categories above filter the cards on the page. Made for a phone first: one card per
 * row there, more on wider screens. The Owner sees the same list and opens a product to read it.
 */
export function ShopProductsPage() {
  const products = useShopProducts();
  const categories = useShopCategories();
  const canEdit = useRoles().includes("Shop");
  const [categoryId, setCategoryId] = useState<string | null>(null);

  const shown =
    products.data?.filter((product) => categoryId === null || product.categoryId === categoryId) ??
    [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="کالاهای فروشگاه"
        description="هر کالا با مدل‌ها، قیمت و موجودی‌اش. کالای پنهان در سایت نشان داده نمی‌شود."
        actions={
          canEdit && (
            <Button asChild>
              <Link to={paths.shopNewProduct}>
                <Plus aria-hidden />
                کالای تازه
              </Link>
            </Button>
          )
        }
      />

      {categories.isSuccess && categories.data.length > 0 && (
        <CategoryFilter
          categories={categories.data}
          selected={categoryId}
          onSelect={setCategoryId}
        />
      )}

      {products.isPending && <LoadingState />}
      {products.isError && <Alert variant="destructive">{errorMessage(products.error)}</Alert>}
      {products.isSuccess && products.data.length === 0 && (
        <EmptyState>
          {canEdit && categories.data?.length === 0 ? (
            <>
              هنوز کالایی نیست. اول در{" "}
              <Link to={paths.shopCategories} className="underline">
                دسته‌بندی‌ها
              </Link>{" "}
              یک دسته بسازید.
            </>
          ) : (
            "هنوز کالایی نیست."
          )}
        </EmptyState>
      )}
      {products.isSuccess && products.data.length > 0 && shown.length === 0 && (
        <EmptyState>در این دسته کالایی نیست.</EmptyState>
      )}

      {shown.length > 0 && (
        <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3" aria-label="فهرست کالاها">
          {shown.map((product) => (
            <li key={product.id}>
              <ProductCard product={product} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** «همه» and one chip per category; they wrap onto more lines on a phone. */
function CategoryFilter({
  categories,
  selected,
  onSelect,
}: {
  categories: ShopCategory[];
  selected: string | null;
  onSelect: (id: string | null) => void;
}) {
  const chip = (active: boolean) =>
    cn(
      "rounded-full border px-3 py-1.5 text-sm transition-colors",
      active ? "border-primary bg-primary text-primary-foreground" : "hover:bg-accent",
    );

  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label="دسته">
      <button
        type="button"
        className={chip(selected === null)}
        aria-pressed={selected === null}
        onClick={() => onSelect(null)}
      >
        همه
      </button>
      {categories.map((category) => (
        <button
          key={category.id}
          type="button"
          className={chip(selected === category.id)}
          aria-pressed={selected === category.id}
          onClick={() => onSelect(category.id)}
        >
          {category.name}
        </button>
      ))}
    </div>
  );
}

/** A price, or the range when the variants differ. */
function priceText(product: ShopProductListItem): string {
  const min = formatMoney(product.minPrice);
  return Number(product.minPrice) === Number(product.maxPrice)
    ? min
    : `${min} تا ${formatMoney(product.maxPrice)}`;
}

/**
 * The photo on the side and the facts beside it: compact on a phone, where a row of cards is read
 * with a thumb. The photo is shown whole (`object-contain`), never cut.
 */
function ProductCard({ product }: { product: ShopProductListItem }) {
  const stock = Number(product.totalStock);
  const variants = Number(product.variantCount);

  return (
    <Link
      to={paths.shopProduct(product.id)}
      className="flex h-full gap-3 rounded-xl border bg-card p-2 transition-colors hover:bg-accent/40 focus-visible:outline-2"
    >
      <div className="grid size-24 shrink-0 place-items-center overflow-hidden rounded-lg bg-muted">
        {product.mainPhotoId ? (
          <img
            src={photoUrl(product.mainPhotoId, "small")}
            alt=""
            loading="lazy"
            className="size-full object-contain"
          />
        ) : (
          <ImageOff aria-hidden className="size-6 text-muted-foreground" />
        )}
      </div>
      <div className="min-w-0 flex-1 space-y-1 py-0.5">
        <div className="flex items-start gap-2">
          <p className="min-w-0 flex-1 font-medium leading-snug break-words">{product.name}</p>
          {!product.isVisible && <Badge variant="secondary">پنهان</Badge>}
        </div>
        <p className="text-xs text-muted-foreground">{product.categoryName}</p>
        <p className="text-sm">{priceText(product)}</p>
        <p className={cn("text-xs", stock === 0 ? "text-destructive" : "text-muted-foreground")}>
          {stock === 0 ? "ناموجود" : `موجودی ${toPersianDigits(stock)}`}
          {variants > 1 && `، ${toPersianDigits(variants)} مدل`}
        </p>
      </div>
    </Link>
  );
}
