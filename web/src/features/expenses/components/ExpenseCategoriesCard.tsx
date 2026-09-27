import { useState } from "react";
import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import {
  useCreateExpenseCategory,
  useExpenseCategories,
  useRenameExpenseCategory,
  type ExpenseCategory,
} from "../api";
import { expenseCategorySchema, type ExpenseCategoryValues } from "../schemas";

const codeFields = { "ExpenseCategories.NameAlreadyExists": "name" } as const;

/**
 * The headings expenses are filed under (BUSINESS_RULES.md §9). The Owner adds and renames them,
 * the eight seeded ones included; there is no delete and no switch, because expenses and reports
 * point at every category ever used.
 */
export function ExpenseCategoriesCard() {
  const categories = useExpenseCategories();
  const [renaming, setRenaming] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  return (
    <Card>
      <CardHeader>
        <CardTitle>دسته‌بندی‌ها</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <NewCategoryForm onDone={setNotice} />

        {notice !== null && (
          <Alert variant="success" role="status">
            {notice}
          </Alert>
        )}

        {categories.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
        {categories.isError && (
          <Alert variant="destructive">{errorMessage(categories.error)}</Alert>
        )}

        {categories.isSuccess && categories.data.length > 0 && (
          <ul className="divide-y" aria-label="فهرست دسته‌بندی‌های هزینه">
            {categories.data.map((category) => (
              <li key={category.id} className="py-2">
                {renaming === category.id ? (
                  <RenameCategoryForm
                    category={category}
                    onDone={() => {
                      setRenaming(null);
                      setNotice("نام دسته‌بندی تغییر کرد.");
                    }}
                    onCancel={() => setRenaming(null)}
                  />
                ) : (
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{category.name}</span>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="ms-auto"
                      aria-label={`تغییر نام ${category.name}`}
                      onClick={() => setRenaming(category.id)}
                    >
                      تغییر نام
                    </Button>
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
  const create = useCreateExpenseCategory();
  const form = useForm<ExpenseCategoryValues>({
    resolver: zodResolver(expenseCategorySchema),
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
  category: ExpenseCategory;
  onDone: () => void;
  onCancel: () => void;
}

function RenameCategoryForm({ category, onDone, onCancel }: RenameCategoryFormProps) {
  const rename = useRenameExpenseCategory();
  const form = useForm<ExpenseCategoryValues>({
    resolver: zodResolver(expenseCategorySchema),
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
