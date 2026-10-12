import { Plus } from "lucide-react";
import { useState } from "react";

import { EmptyState, LoadingState } from "@/components/States";
import { SelectField } from "@/components/FormField";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { filterEdge, toneAction } from "@/lib/edges";
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
import { switchClass } from "../stockSwitch";
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
    <Card edge>
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
          // Edged in the page's tone like «عضو جدید» (13.2i).
          <Button
            variant="ghost"
            className={toneAction}
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
          // Edged like the cards around it (13.2i), not a grey frame of its own.
          <div className="rounded-md border border-s-[3px] p-3 tone-edge">
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

        {/* A filter box like the history's (13.2i). */}
        <div className="w-full sm:w-64">
          <SelectField
            label="نمایش دسته‌بندی"
            className={filterEdge}
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

        {products.isPending && <LoadingState />}
        {products.isError && <Alert variant="destructive">{errorMessage(products.error)}</Alert>}
        {products.isSuccess && products.data.items.length === 0 && (
          <EmptyState>محصولی در این فهرست نیست.</EmptyState>
        )}

        {products.isSuccess && products.data.items.length > 0 && (
          <>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>نام</TableHead>
                  <TableHead>دسته‌بندی</TableHead>
                  <TableHead>قیمت</TableHead>
                  <TableHead>وضعیت</TableHead>
                  <TableHead>عملیات</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {products.data.items.map((product) =>
                  editing === product.id ? (
                    <TableRow key={product.id} className="bg-muted/30">
                      <TableCell colSpan={5} className="py-2">
                        <ProductForm
                          defaultValues={{
                            name: product.name,
                            categoryId: product.categoryId,
                            price: String(product.price),
                          }}
                          categories={categoryList}
                          submitLabel="ذخیره"
                          editing
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
                      </TableCell>
                    </TableRow>
                  ) : (
                    <TableRow key={product.id}>
                      <TableCell className="font-medium tone-row-edge">{product.name}</TableCell>
                      <TableCell label="دسته‌بندی">{product.categoryName}</TableCell>
                      <TableCell label="قیمت">{formatMoney(product.price)}</TableCell>
                      <TableCell label="وضعیت">
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
                      </TableCell>
                      <TableCell>
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
                            variant="ghost"
                            className={switchClass(product.isActive)}
                            disabled={setActive.isPending}
                            aria-label={`${product.isActive ? "ناموجود کردن" : "موجود کردن"} ${product.name}`}
                            onClick={() => void toggleActive(product)}
                          >
                            {product.isActive ? "ناموجود" : "موجود"}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ),
                )}
              </TableBody>
            </Table>
            <Pager page={page} pageCount={products.data.pageCount} onPageChange={setPage} />
          </>
        )}
      </CardContent>
    </Card>
  );
}
