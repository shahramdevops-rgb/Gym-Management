import { Plus } from "lucide-react";
import { useState } from "react";

import { SelectField } from "@/components/FormField";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { normalizeMoney } from "@/lib/money";

import {
  useCreateProduct,
  useProductCategories,
  useProductList,
  useSetProductActive,
  useUpdateProduct,
  type Product,
} from "../api";
import { emptyProductValues } from "../schemas";
import { ProductForm } from "./ProductForm";
import { StockBadge } from "./StockBadge";

type Notice = { kind: "success" | "destructive"; text: string };

/**
 * The price list, everything on it whether it can be sold today or not, with both switches
 * (BUSINESS_RULES.md §8). A product is switched off, never deleted: orders already sold point at
 * it.
 */
export function ProductsCard() {
  const categories = useProductCategories();
  const [categoryId, setCategoryId] = useState("");
  const [page, setPage] = useState(1);
  const products = useProductList({ categoryId: categoryId || undefined, page });
  const createProduct = useCreateProduct();
  const updateProduct = useUpdateProduct();
  const setActive = useSetProductActive();

  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);

  const categoryList = categories.data ?? [];

  async function toggleActive(product: Product) {
    setNotice(null);
    try {
      await setActive.mutateAsync({ id: product.id, active: !product.isActive });
      setNotice({
        kind: "success",
        text: product.isActive ? `«${product.name}» ناموجود شد.` : `«${product.name}» موجود شد.`,
      });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    }
  }

  return (
    <Card>
      <CardHeader className="flex flex-wrap items-center justify-between gap-3">
        <CardTitle>
          محصولات
          {products.isSuccess && (
            <span className="ms-2 text-sm font-normal text-muted-foreground">
              ({toPersianDigits(products.data.totalCount)})
            </span>
          )}
        </CardTitle>
        {!adding && (
          <Button
            size="sm"
            disabled={categoryList.length === 0}
            onClick={() => {
              setNotice(null);
              setAdding(true);
            }}
          >
            <Plus aria-hidden />
            محصول جدید
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        {categories.isSuccess && categoryList.length === 0 && (
          <p className="text-sm text-muted-foreground">
            برای افزودن محصول، اول یک دسته‌بندی بسازید.
          </p>
        )}

        {adding && (
          <div className="rounded-md border p-3">
            <ProductForm
              defaultValues={{
                ...emptyProductValues,
                categoryId: categoryId || (categoryList[0]?.id ?? ""),
              }}
              categories={categoryList}
              submitLabel="افزودن محصول"
              onSubmit={async (values) => {
                await createProduct.mutateAsync({
                  name: values.name,
                  categoryId: values.categoryId,
                  price: normalizeMoney(values.price),
                });
                setAdding(false);
                setNotice({ kind: "success", text: `«${values.name}» به منو اضافه شد.` });
              }}
              onCancel={() => setAdding(false)}
            />
          </div>
        )}

        {notice !== null && (
          <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
            {notice.text}
          </Alert>
        )}

        <div className="max-w-xs">
          <SelectField
            label="نمایش دسته‌بندی"
            value={categoryId}
            onChange={(event) => {
              setCategoryId(event.target.value);
              setPage(1);
            }}
          >
            <option value="">همهٔ دسته‌بندی‌ها</option>
            {categoryList.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </SelectField>
        </div>

        {products.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
        {products.isError && <Alert variant="destructive">{errorMessage(products.error)}</Alert>}
        {products.isSuccess && products.data.items.length === 0 && (
          <p className="text-muted-foreground">محصولی در این فهرست نیست.</p>
        )}

        {products.isSuccess && products.data.items.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-muted-foreground">
                    <th className="py-2 text-start font-medium">نام</th>
                    <th className="py-2 text-start font-medium">دسته‌بندی</th>
                    <th className="py-2 text-start font-medium">قیمت</th>
                    <th className="py-2 text-start font-medium">وضعیت</th>
                    <th className="py-2 text-start font-medium">عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {products.data.items.map((product) =>
                    editing === product.id ? (
                      <tr key={product.id} className="border-b bg-muted/30">
                        <td colSpan={5} className="py-2">
                          <ProductForm
                            defaultValues={{
                              name: product.name,
                              categoryId: product.categoryId,
                              price: String(product.price),
                            }}
                            categories={categoryList}
                            submitLabel="ذخیره"
                            onSubmit={async (values) => {
                              // The version read with the list, so an edit that crossed
                              // somebody else's is refused rather than silently winning.
                              await updateProduct.mutateAsync({
                                id: product.id,
                                name: values.name,
                                categoryId: values.categoryId,
                                price: normalizeMoney(values.price),
                                version: product.version,
                              });
                              setEditing(null);
                              setNotice({ kind: "success", text: `«${values.name}» ذخیره شد.` });
                            }}
                            onCancel={() => setEditing(null)}
                          />
                        </td>
                      </tr>
                    ) : (
                      <tr key={product.id} className="border-b">
                        <td className="py-2 font-medium">{product.name}</td>
                        <td className="py-2">{product.categoryName}</td>
                        <td className="py-2">{formatMoney(product.price)}</td>
                        <td className="py-2">
                          <div className="flex flex-wrap items-center gap-2">
                            <StockBadge inStock={product.isActive} />
                            {product.isActive && !product.categoryIsActive && (
                              // Switched on, but its shelf is off: the till will not offer it,
                              // and the screen says why instead of showing a موجود it cannot sell.
                              <span className="text-xs text-muted-foreground">
                                دسته‌بندی ناموجود است؛ در بوفه نمایش داده نمی‌شود.
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="py-2">
                          <div className="flex flex-wrap gap-1">
                            <Button
                              size="sm"
                              variant="ghost"
                              aria-label={`ویرایش ${product.name}`}
                              onClick={() => {
                                setNotice(null);
                                setEditing(product.id);
                              }}
                            >
                              ویرایش
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={setActive.isPending}
                              aria-label={`${product.isActive ? "ناموجود کردن" : "موجود کردن"} ${product.name}`}
                              onClick={() => void toggleActive(product)}
                            >
                              {product.isActive ? "ناموجود" : "موجود"}
                            </Button>
                          </div>
                        </td>
                      </tr>
                    ),
                  )}
                </tbody>
              </table>
            </div>
            <Pager page={page} pageCount={products.data.pageCount} onPageChange={setPage} />
          </>
        )}
      </CardContent>
    </Card>
  );
}
