import { ArrowRight, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { Controller, useFieldArray, useForm } from "react-hook-form";
import { Link, useLocation, useNavigate, useParams } from "react-router";

import { FormField, MoneyField, SelectField, TextareaField } from "@/components/FormField";
import { PageHeader } from "@/components/PageHeader";
import { SaveRow, StaleAlert, type SaveNotice } from "@/components/SaveRow";
import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { paths } from "@/app/paths";
import { useRoles } from "@/features/auth/api";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { applyServerErrors, withPersianDigits, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

import {
  useCreateShopProduct,
  useShopCategories,
  useShopProduct,
  useUpdateShopProduct,
  type ShopCategory,
  type ShopProduct,
  type ShopProductInput,
} from "../api";
import { ProductPhotos } from "../components/ProductPhotos";
import { catalogLimits, productSchema, stockNumber, type ProductValues } from "../schemas";

const changedConcurrently = "ShopProducts.ChangedConcurrently";

const codeFields = {
  "ShopProducts.NameAlreadyExists": "name",
  "ShopProducts.CategoryNotFound": "categoryId",
} as const;

function isCode(problem: unknown, code: string): boolean {
  return (
    typeof problem === "object" && problem !== null && "code" in problem && problem.code === code
  );
}

/** The request a save sends: money and stock as the API reads them, whichever digits were typed. */
function toInput(values: ProductValues): ShopProductInput {
  return {
    name: values.name.trim(),
    description: values.description.trim() === "" ? null : values.description.trim(),
    categoryId: values.categoryId,
    isVisible: values.isVisible,
    variants: values.variants.map((variant) => ({
      id: variant.id,
      label: variant.label.trim(),
      price: normalizeMoney(variant.price),
      stock: stockNumber(variant.stock) ?? 0,
    })),
  };
}

function fromProduct(product: ShopProduct): ProductValues {
  return {
    name: product.name,
    description: product.description ?? "",
    categoryId: product.categoryId,
    isVisible: product.isVisible,
    variants: product.variants.map((variant) => ({
      id: variant.id,
      label: variant.label,
      price: String(variant.price),
      stock: toPersianDigits(Number(variant.stock)),
    })),
  };
}

const emptyVariant = { id: null, label: "", price: "", stock: "" };

/**
 * A product of the online shop (BUSINESS_RULES.md §16 *Products*): `/shop/products/new` adds one,
 * `/shop/products/:id` opens it. The shop admin edits its details and variants in one form and its
 * photos below (a new product gets photos once it is saved); the Owner reads it.
 */
export function ShopProductPage() {
  const { id } = useParams();
  return id === undefined ? <NewProduct /> : <ExistingProduct id={id} />;
}

function BackLink() {
  return (
    <Button asChild variant="ghost" size="sm">
      <Link to={paths.shopProducts}>
        <ArrowRight aria-hidden />
        کالاها
      </Link>
    </Button>
  );
}

function NewProduct() {
  const categories = useShopCategories();
  const create = useCreateShopProduct();
  const navigate = useNavigate();

  if (categories.isPending) {
    return <LoadingState />;
  }
  if (categories.isError) {
    return <Alert variant="destructive">{errorMessage(categories.error)}</Alert>;
  }

  return (
    <div className="max-w-3xl space-y-6">
      <BackLink />
      <PageHeader title="کالای تازه" description="بعد از ذخیره، عکس‌های کالا را اضافه کنید." />
      {categories.data.length === 0 ? (
        <Alert>
          اول در{" "}
          <Link to={paths.shopCategories} className="underline">
            دسته‌بندی‌ها
          </Link>{" "}
          یک دسته بسازید.
        </Alert>
      ) : (
        <ProductForm
          categories={categories.data}
          initial={{
            name: "",
            description: "",
            categoryId: "",
            isVisible: true,
            variants: [emptyVariant],
          }}
          submitLabel="ذخیره و افزودن عکس"
          onSubmit={async (input) => {
            const product = await create.mutateAsync(input);
            void navigate(paths.shopProduct(product.id), { state: { created: true } });
          }}
        />
      )}
    </div>
  );
}

function ExistingProduct({ id }: { id: string }) {
  const product = useShopProduct(id);
  const categories = useShopCategories();
  const canEdit = useRoles().includes("Shop");
  const created = (useLocation().state as { created?: boolean } | null)?.created === true;

  if (product.isPending || categories.isPending) {
    return <LoadingState />;
  }
  if (product.isError) {
    return <Alert variant="destructive">{errorMessage(product.error)}</Alert>;
  }
  if (categories.isError) {
    return <Alert variant="destructive">{errorMessage(categories.error)}</Alert>;
  }

  return (
    <div className="max-w-3xl space-y-6">
      <BackLink />
      <PageHeader
        title={product.data.name}
        description={
          canEdit ? "مشخصات، مدل‌ها و عکس‌های کالا." : "این‌ها را فقط مدیر فروشگاه تغییر می‌دهد."
        }
        actions={!product.data.isVisible && <Badge variant="secondary">پنهان در سایت</Badge>}
      />
      {created && (
        <Alert variant="success" role="status">
          کالا ذخیره شد. حالا عکس‌هایش را اضافه کنید.
        </Alert>
      )}
      <ProductPhotos product={product.data} canEdit={canEdit} />
      {canEdit ? (
        <EditProductForm product={product.data} categories={categories.data} />
      ) : (
        <ProductDetails product={product.data} categories={categories.data} />
      )}
    </div>
  );
}

function EditProductForm({
  product,
  categories,
}: {
  product: ShopProduct;
  categories: ShopCategory[];
}) {
  const update = useUpdateShopProduct(product.id);
  const [notice, setNotice] = useState<SaveNotice>(null);
  const [stale, setStale] = useState(false);

  return (
    <ProductForm
      // Fresh values after a save or a reload rebuild the form with them.
      key={String(product.version)}
      categories={categories}
      initial={fromProduct(product)}
      submitLabel="ذخیره"
      notice={notice}
      stale={
        stale ? (
          <StaleAlert
            code={changedConcurrently}
            reloading={false}
            onReload={() => window.location.reload()}
          />
        ) : undefined
      }
      onSubmit={async (input) => {
        setNotice(null);
        try {
          await update.mutateAsync({ ...input, version: product.version });
          setNotice("saved");
        } catch (problem) {
          if (isCode(problem, changedConcurrently)) {
            setStale(true);
            return;
          }
          throw problem;
        }
      }}
    />
  );
}

/**
 * The details and the variants in one form. On a phone each variant is a small box of its own
 * with its fields one under another; from `sm` up its three fields share a line.
 */
function ProductForm({
  categories,
  initial,
  submitLabel,
  notice = null,
  stale,
  onSubmit,
}: {
  categories: ShopCategory[];
  initial: ProductValues;
  submitLabel: string;
  notice?: SaveNotice;
  stale?: React.ReactNode;
  onSubmit: (input: ShopProductInput) => Promise<void>;
}) {
  const form = useForm<ProductValues>({
    resolver: zodResolver(productSchema),
    defaultValues: initial,
  });
  const variants = useFieldArray({ control: form.control, name: "variants" });
  const errors = form.formState.errors;
  const single = variants.fields.length === 1;

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit(toInput(values));
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  return (
    <form onSubmit={(event) => void submit(event)} className="space-y-6" noValidate>
      <Card>
        <CardHeader>
          <CardTitle>مشخصات</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FormField label="نام کالا" error={errors.name?.message} {...form.register("name")} />
          <SelectField
            label="دسته"
            error={errors.categoryId?.message}
            {...form.register("categoryId")}
          >
            <option value="">انتخاب کنید</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </SelectField>
          <TextareaField
            label="توضیح (اختیاری)"
            rows={4}
            maxLength={catalogLimits.description}
            error={errors.description?.message}
            {...form.register("description")}
          />
          <label className="flex min-h-11 items-center gap-3 text-sm">
            <input
              type="checkbox"
              className="size-5 accent-primary"
              {...form.register("isVisible")}
            />
            نمایش در سایت
          </label>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>مدل، قیمت و موجودی</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">
            هر مدل قیمت و موجودی خودش را دارد، مثل «شکلاتی، ۲ کیلو». کالایی که یک مدل دارد نام مدل
            لازم ندارد.
          </p>
          {errors.variants?.root?.message !== undefined && (
            <Alert variant="destructive">{errors.variants.root.message}</Alert>
          )}
          <ul className="space-y-3">
            {variants.fields.map((field, index) => (
              <li key={field.id} className="rounded-lg border p-3">
                <div className="grid gap-3 sm:grid-cols-[1fr_1fr_7rem] sm:items-start">
                  <FormField
                    label={single ? "نام مدل (اختیاری)" : `نام مدل ${toPersianDigits(index + 1)}`}
                    placeholder="مثلاً شکلاتی، ۲ کیلو"
                    error={errors.variants?.[index]?.label?.message}
                    {...form.register(`variants.${index}.label`)}
                  />
                  <Controller
                    control={form.control}
                    name={`variants.${index}.price`}
                    render={({ field: price }) => (
                      <MoneyField
                        label="قیمت (تومان)"
                        name={price.name}
                        value={price.value}
                        onChange={price.onChange}
                        onBlur={price.onBlur}
                        error={errors.variants?.[index]?.price?.message}
                      />
                    )}
                  />
                  {/* A number of a few digits: a short box on a phone too. */}
                  <div className="max-w-40 sm:max-w-none">
                    <FormField
                      label="موجودی"
                      inputMode="numeric"
                      dir="ltr"
                      error={errors.variants?.[index]?.stock?.message}
                      {...withPersianDigits(form.register(`variants.${index}.stock`))}
                    />
                  </div>
                </div>
                {!single && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="mt-2 text-destructive hover:bg-destructive/10 hover:text-destructive"
                    onClick={() => variants.remove(index)}
                  >
                    <Trash2 aria-hidden />
                    حذف این مدل
                  </Button>
                )}
              </li>
            ))}
          </ul>
          {variants.fields.length < catalogLimits.variants && (
            <Button type="button" variant="outline" onClick={() => variants.append(emptyVariant)}>
              <Plus aria-hidden />
              افزودن مدل
            </Button>
          )}
        </CardContent>
      </Card>

      <SaveRow error={errors.root?.server?.message} notice={notice} stale={stale}>
        <Button type="submit" disabled={form.formState.isSubmitting}>
          {submitLabel}
        </Button>
      </SaveRow>
    </form>
  );
}

/** The Owner's view: the details and each variant's price and stock, to read. */
function ProductDetails({
  product,
  categories,
}: {
  product: ShopProduct;
  categories: ShopCategory[];
}) {
  const category = categories.find((c) => c.id === product.categoryId)?.name ?? "";

  return (
    <Card>
      <CardContent className="space-y-4">
        <dl className="grid gap-3 sm:grid-cols-2">
          <div>
            <dt className="text-xs text-muted-foreground">دسته</dt>
            <dd className="font-medium">{category}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">در سایت</dt>
            <dd className="font-medium">{product.isVisible ? "نمایش داده می‌شود" : "پنهان"}</dd>
          </div>
        </dl>
        {product.description !== null && (
          <p className="text-sm leading-7 whitespace-pre-line">{product.description}</p>
        )}
        <ul className="divide-y rounded-lg border">
          {product.variants.map((variant) => (
            <li
              key={variant.id}
              className="flex flex-wrap items-center gap-x-4 gap-y-1 px-3 py-2 text-sm"
            >
              <span className="min-w-0 flex-1 font-medium">
                {variant.label === "" ? "تک‌مدل" : variant.label}
              </span>
              <span>{formatMoney(variant.price)}</span>
              <span
                className={
                  Number(variant.stock) === 0 ? "text-destructive" : "text-muted-foreground"
                }
              >
                {Number(variant.stock) === 0
                  ? "ناموجود"
                  : `موجودی ${toPersianDigits(Number(variant.stock))}`}
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
