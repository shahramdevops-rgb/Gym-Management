import { useState } from "react";
import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import {
  useCreateProductCategory,
  useDeleteProductCategory,
  useProductCategories,
  useRenameProductCategory,
  useSetProductCategoryActive,
  type ProductCategory,
} from "../api";
import { categorySchema, type CategoryValues } from "../schemas";
import { StockBadge } from "./StockBadge";

const codeFields = { "ProductCategories.NameAlreadyExists": "name" } as const;

type Notice = { kind: "success" | "destructive"; text: string };

/**
 * The menu's headings (BUSINESS_RULES.md §8). A category is switched off for "not today" — its
 * whole shelf leaves the till at once — and deleted only for "this was a mistake", which the API
 * allows only while no product uses it.
 */
export function CategoriesCard() {
  const categories = useProductCategories();
  const setActive = useSetProductCategoryActive();
  const deleteCategory = useDeleteProductCategory();
  const [renaming, setRenaming] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);

  async function run(action: () => Promise<unknown>, success: string) {
    setNotice(null);
    try {
      await action();
      setNotice({ kind: "success", text: success });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>دسته‌بندی‌ها</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <NewCategoryForm onDone={(text) => setNotice({ kind: "success", text })} />

        {notice !== null && (
          <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
            {notice.text}
          </Alert>
        )}

        {categories.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
        {categories.isError && (
          <Alert variant="destructive">{errorMessage(categories.error)}</Alert>
        )}
        {categories.isSuccess && categories.data.length === 0 && (
          <p className="text-muted-foreground">هنوز دسته‌بندی‌ای تعریف نشده است.</p>
        )}

        {categories.isSuccess && categories.data.length > 0 && (
          <ul className="divide-y" aria-label="فهرست دسته‌بندی‌ها">
            {categories.data.map((category) => (
              <li key={category.id} className="py-2">
                {renaming === category.id ? (
                  <RenameCategoryForm
                    category={category}
                    onDone={() => {
                      setRenaming(null);
                      setNotice({ kind: "success", text: "نام دسته‌بندی تغییر کرد." });
                    }}
                    onCancel={() => setRenaming(null)}
                  />
                ) : (
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{category.name}</span>
                    <StockBadge inStock={category.isActive} />
                    <div className="ms-auto flex flex-wrap gap-1">
                      <Button
                        size="sm"
                        variant="ghost"
                        aria-label={`تغییر نام ${category.name}`}
                        onClick={() => setRenaming(category.id)}
                      >
                        تغییر نام
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={setActive.isPending}
                        aria-label={`${category.isActive ? "ناموجود کردن" : "موجود کردن"} ${category.name}`}
                        onClick={() =>
                          void run(
                            () =>
                              setActive.mutateAsync({
                                id: category.id,
                                active: !category.isActive,
                              }),
                            category.isActive
                              ? `«${category.name}» و همهٔ محصولاتش از بوفه برداشته شد.`
                              : `«${category.name}» دوباره موجود شد.`,
                          )
                        }
                      >
                        {category.isActive ? "ناموجود" : "موجود"}
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-destructive"
                        disabled={deleteCategory.isPending}
                        aria-label={`حذف ${category.name}`}
                        onClick={() =>
                          void run(
                            () => deleteCategory.mutateAsync(category.id),
                            `«${category.name}» حذف شد.`,
                          )
                        }
                      >
                        حذف
                      </Button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function NewCategoryForm({ onDone }: { onDone: (message: string) => void }) {
  const create = useCreateProductCategory();
  const form = useForm<CategoryValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: "" },
  });

  const submit = form.handleSubmit(async (values) => {
    try {
      await create.mutateAsync(values.name);
      form.reset();
      onDone(`دسته‌بندی «${values.name}» اضافه شد.`);
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="flex items-start gap-3" onSubmit={submit} noValidate>
      <div className="flex-1">
        <FormField
          label="دسته‌بندی جدید"
          autoComplete="off"
          error={errors.name?.message ?? errors.root?.server?.message}
          {...form.register("name")}
        />
      </div>
      <Button type="submit" className="mt-[1.375rem]" disabled={isSubmitting}>
        افزودن
      </Button>
    </form>
  );
}

interface RenameCategoryFormProps {
  category: ProductCategory;
  onDone: () => void;
  onCancel: () => void;
}

function RenameCategoryForm({ category, onDone, onCancel }: RenameCategoryFormProps) {
  const rename = useRenameProductCategory();
  const form = useForm<CategoryValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: category.name },
  });

  const submit = form.handleSubmit(async (values) => {
    try {
      // The version read with the list: a rename that crossed somebody else's is refused.
      await rename.mutateAsync({ id: category.id, name: values.name, version: category.version });
      onDone();
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="flex items-start gap-2" onSubmit={submit} noValidate>
      <div className="flex-1">
        <FormField
          label={`نام تازهٔ ${category.name}`}
          autoComplete="off"
          autoFocus
          error={errors.name?.message ?? errors.root?.server?.message}
          {...form.register("name")}
        />
      </div>
      <Button type="submit" size="sm" className="mt-[1.375rem]" disabled={isSubmitting}>
        ذخیره
      </Button>
      <Button type="button" size="sm" variant="ghost" className="mt-[1.375rem]" onClick={onCancel}>
        انصراف
      </Button>
    </form>
  );
}
