import { useState } from "react";
import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { useRoles } from "@/features/auth/api";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import {
  useCreateShopCategory,
  useDeleteShopCategory,
  useRenameShopCategory,
  useShopCategories,
  type ShopCategory,
} from "../api";
import { categorySchema, type CategoryValues } from "../schemas";

const codeFields = { "ShopCategories.NameAlreadyExists": "name" } as const;

type Notice = { kind: "success" | "destructive"; text: string };

/**
 * «دسته‌بندی‌ها» of the online shop (BUSINESS_RULES.md §16 *Products*): one level, each product in
 * one. The shop admin adds, renames and deletes them (deleting only while empty); the Owner reads.
 * One short list that works the same on a phone: the add box on top, a line per category below.
 */
export function ShopCategoriesPage() {
  const categories = useShopCategories();
  const canEdit = useRoles().includes("Shop");
  const [notice, setNotice] = useState<Notice | null>(null);

  return (
    <div className="max-w-2xl space-y-6">
      <PageHeader
        title="دسته‌بندی‌های فروشگاه"
        description="هر کالا در یک دسته است. دسته‌ای که کالا دارد پاک نمی‌شود."
      />
      <Card>
        <CardContent className="space-y-4">
          {canEdit && <NewCategoryForm onDone={(text) => setNotice({ kind: "success", text })} />}

          {notice !== null && (
            <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
              {notice.text}
            </Alert>
          )}

          {categories.isPending && <LoadingState />}
          {categories.isError && (
            <Alert variant="destructive">{errorMessage(categories.error)}</Alert>
          )}
          {categories.isSuccess && categories.data.length === 0 && (
            <EmptyState>هنوز دسته‌ای نیست.</EmptyState>
          )}
          {categories.isSuccess && categories.data.length > 0 && (
            <ul className="divide-y" aria-label="فهرست دسته‌ها">
              {categories.data.map((category) => (
                <CategoryRow
                  key={category.id}
                  category={category}
                  canEdit={canEdit}
                  onNotice={setNotice}
                />
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function CategoryRow({
  category,
  canEdit,
  onNotice,
}: {
  category: ShopCategory;
  canEdit: boolean;
  onNotice: (notice: Notice | null) => void;
}) {
  const [renaming, setRenaming] = useState(false);
  const remove = useDeleteShopCategory();
  const count = Number(category.productCount);

  if (renaming) {
    return (
      <li className="py-3">
        <RenameForm
          category={category}
          onDone={() => {
            setRenaming(false);
            onNotice({ kind: "success", text: "نام دسته تغییر کرد." });
          }}
          onCancel={() => setRenaming(false)}
        />
      </li>
    );
  }

  return (
    <li className="flex flex-wrap items-center gap-2 py-3">
      <span className="font-medium">{category.name}</span>
      <span className="text-sm text-muted-foreground">
        {count === 0 ? "بدون کالا" : `${toPersianDigits(count)} کالا`}
      </span>
      {canEdit && (
        <div className="ms-auto flex gap-1">
          <Button
            size="sm"
            variant="ghost"
            aria-label={`تغییر نام ${category.name}`}
            onClick={() => setRenaming(true)}
          >
            تغییر نام
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className="text-destructive hover:bg-destructive/10 hover:text-destructive"
            disabled={remove.isPending || count > 0}
            title={count > 0 ? "دسته‌ای که کالا دارد پاک نمی‌شود" : undefined}
            aria-label={`حذف ${category.name}`}
            onClick={() => {
              onNotice(null);
              remove.mutate(category.id, {
                onSuccess: () => onNotice({ kind: "success", text: `«${category.name}» پاک شد.` }),
                onError: (problem) =>
                  onNotice({ kind: "destructive", text: errorMessage(problem) }),
              });
            }}
          >
            حذف
          </Button>
        </div>
      )}
    </li>
  );
}

function NewCategoryForm({ onDone }: { onDone: (message: string) => void }) {
  const create = useCreateShopCategory();
  const form = useForm<CategoryValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: "" },
  });

  const submit = form.handleSubmit(async (values) => {
    try {
      await create.mutateAsync(values.name);
      form.reset();
      onDone(`دستهٔ «${values.name}» اضافه شد.`);
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  return (
    <form
      onSubmit={(event) => void submit(event)}
      className="flex flex-wrap items-end gap-2"
      noValidate
    >
      <div className="min-w-0 flex-1 basis-48">
        <FormField
          label="دستهٔ تازه"
          placeholder="مثلاً مکمل"
          error={form.formState.errors.name?.message}
          {...form.register("name")}
        />
      </div>
      <Button type="submit" disabled={create.isPending}>
        افزودن
      </Button>
    </form>
  );
}

function RenameForm({
  category,
  onDone,
  onCancel,
}: {
  category: ShopCategory;
  onDone: () => void;
  onCancel: () => void;
}) {
  const rename = useRenameShopCategory();
  const form = useForm<CategoryValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: category.name },
  });

  const submit = form.handleSubmit(async (values) => {
    try {
      await rename.mutateAsync({ id: category.id, name: values.name, version: category.version });
      onDone();
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  return (
    <form
      onSubmit={(event) => void submit(event)}
      className="flex flex-wrap items-end gap-2"
      noValidate
    >
      <div className="min-w-0 flex-1 basis-48">
        <FormField
          label={`نام تازهٔ «${category.name}»`}
          error={form.formState.errors.name?.message ?? form.formState.errors.root?.message}
          {...form.register("name")}
        />
      </div>
      <Button type="submit" disabled={rename.isPending}>
        ذخیره
      </Button>
      <Button type="button" variant="ghost" onClick={onCancel}>
        انصراف
      </Button>
    </form>
  );
}
