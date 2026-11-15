import { ChevronRight, ImageOff } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";

import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { errorMessage, type ApiProblem } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import {
  isProductId,
  showcasePhotoUrl,
  useShowcaseProduct,
  type ShowcaseProduct,
  type ShowcaseVariant,
} from "./api";
import { ContactWays } from "./ContactWays";
import { shopPaths } from "./paths";
import { ShopFrame } from "./ShopFrame";
import { shopTitle } from "./ShowcasePage";

/**
 * One product's page (BUSINESS_RULES.md §16 *The showcase*): its photos, its variants with each
 * one's price and whether it is in stock, its description, and the shop's contact ways. A product
 * the shop admin hid, or an address that names none, is «این کالا پیدا نشد».
 */
export function ProductPage() {
  const { productId = "" } = useParams();
  const product = useShowcaseProduct(productId);

  const name = product.data?.name;
  useEffect(() => {
    document.title = name === undefined ? shopTitle : `${name} | فروشگاه پاسارگاد پلاس`;
  }, [name]);

  // Not an id at all (an address cut short), or the API's «no such shown product».
  const notFound =
    !isProductId(productId) ||
    (product.isError && (product.error as ApiProblem | null)?.status === 404);

  return (
    <ShopFrame>
      <Link
        to={shopPaths.showcase}
        className="inline-flex items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ChevronRight aria-hidden className="size-4" />
        همهٔ کالاها
      </Link>

      {product.isPending && !notFound && <LoadingState />}
      {notFound && <EmptyState>این کالا پیدا نشد.</EmptyState>}
      {product.isError && !notFound && (
        <Alert variant="destructive">{errorMessage(product.error)}</Alert>
      )}

      {product.isSuccess && (
        <>
          <div className="grid gap-8 lg:grid-cols-2 lg:gap-12">
            {/* Remounted for another product, so its first photo is the one shown. */}
            <Photos key={product.data.id} product={product.data} />
            <Details product={product.data} />
          </div>

          <ContactWays contacts={product.data.contacts} />
        </>
      )}
    </ShopFrame>
  );
}

/**
 * The chosen photo whole on a square (never cut), and the others under it as small squares that
 * choose it. A product without a photo keeps the square, so the page does not jump.
 */
function Photos({ product }: { product: ShowcaseProduct }) {
  const [chosen, setChosen] = useState(0);
  const photo = product.photos[chosen];

  return (
    <div className="space-y-3">
      <div className="grid aspect-square place-items-center overflow-hidden rounded-md border bg-card">
        {photo === undefined ? (
          <ImageOff aria-hidden className="size-10 text-muted-foreground" />
        ) : (
          <img
            src={showcasePhotoUrl(photo.id, "large")}
            alt={product.name}
            width={Number(photo.width)}
            height={Number(photo.height)}
            className="size-full object-contain"
          />
        )}
      </div>

      {product.photos.length > 1 && (
        <ul className="grid grid-cols-5 gap-2 sm:grid-cols-6" aria-label="عکس‌های کالا">
          {product.photos.map((item, index) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => setChosen(index)}
                aria-label={`عکس ${toPersianDigits(index + 1)}`}
                aria-pressed={index === chosen}
                className={cn(
                  "block aspect-square w-full overflow-hidden rounded-sm border bg-card transition-colors focus-visible:outline-2 focus-visible:outline-ring",
                  index === chosen ? "border-primary" : "hover:border-primary/60",
                )}
              >
                <img
                  src={showcasePhotoUrl(item.id, "small")}
                  alt=""
                  loading="lazy"
                  className="size-full object-contain"
                />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function Details({ product }: { product: ShowcaseProduct }) {
  const single = product.variants.length === 1 ? product.variants[0] : undefined;

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        {/* The category is the way back to its own products. */}
        <Link
          to={shopPaths.category(product.categoryId)}
          className="text-sm font-semibold text-primary hover:underline"
        >
          {product.categoryName}
        </Link>
        <h1 className="text-2xl leading-snug font-extrabold break-words sm:text-3xl sm:leading-snug">
          {product.name}
        </h1>
      </div>

      {single === undefined ? (
        <ul className="grid gap-2 sm:grid-cols-2" aria-label="مدل‌ها و قیمت">
          {product.variants.map((variant) => (
            <li key={variant.id}>
              <VariantBox variant={variant} />
            </li>
          ))}
        </ul>
      ) : (
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          {single.label !== "" && <p className="w-full text-sm">{single.label}</p>}
          <p
            className={cn(
              "text-2xl font-extrabold",
              single.inStock ? "text-primary" : "text-muted-foreground",
            )}
          >
            {formatMoney(single.price)}
          </p>
          <StockNote inStock={single.inStock} />
        </div>
      )}

      {product.description !== null && (
        <p className="leading-loose break-words whitespace-pre-line text-muted-foreground">
          {product.description}
        </p>
      )}
    </div>
  );
}

/** One variant as a small box: its label, its own price, and whether it is in stock. */
function VariantBox({ variant }: { variant: ShowcaseVariant }) {
  return (
    <div
      className={cn("h-full rounded-md border bg-card px-3 py-2", !variant.inStock && "opacity-70")}
    >
      <p className="text-sm break-words">{variant.label}</p>
      <div className="flex flex-wrap items-baseline justify-between gap-x-3">
        <p className={cn("font-bold", variant.inStock ? "text-primary" : "text-muted-foreground")}>
          {formatMoney(variant.price)}
        </p>
        <StockNote inStock={variant.inStock} />
      </div>
    </div>
  );
}

/** In stock or not, in words; never how many (§16). */
function StockNote({ inStock }: { inStock: boolean }) {
  return (
    <span className={cn("text-xs", inStock ? "text-success-ink" : "text-muted-foreground")}>
      {inStock ? "موجود" : "ناموجود"}
    </span>
  );
}
