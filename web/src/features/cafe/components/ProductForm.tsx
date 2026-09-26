import { Controller, useForm } from "react-hook-form";

import { FormField, MoneyField, SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import type { ProductCategory } from "../api";
import { productSchema, type ProductValues } from "../schemas";

const codeFields = {
  "Products.NameAlreadyExists": "name",
  "Products.CategoryNotFound": "categoryId",
} as const;

interface ProductFormProps {
  defaultValues: ProductValues;
  categories: ProductCategory[];
  submitLabel: string;
  /** Sends the form; a rejection is a server problem and lands on the form's fields. */
  onSubmit: (values: ProductValues) => Promise<void>;
  onCancel: () => void;
}

/**
 * A product's name, category and price, for adding one and for editing it. The price goes
 * through `MoneyField` like every amount in the app, and follows a plan's price rules: at most
 * two decimals, refused rather than rounded, never negative (BUSINESS_RULES.md §8).
 */
export function ProductForm({
  defaultValues,
  categories,
  submitLabel,
  onSubmit,
  onCancel,
}: ProductFormProps) {
  const form = useForm({ resolver: zodResolver(productSchema), defaultValues });

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit(values);
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="flex flex-wrap items-start gap-3" onSubmit={submit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive" className="w-full">
          {errors.root.server.message}
        </Alert>
      )}

      <div className="min-w-48 flex-1">
        <FormField
          label="نام محصول"
          autoComplete="off"
          error={errors.name?.message}
          {...form.register("name")}
        />
      </div>
      <div className="w-48">
        <SelectField
          label="دسته‌بندی"
          error={errors.categoryId?.message}
          {...form.register("categoryId")}
        >
          <option value="">انتخاب کنید…</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.isActive ? category.name : `${category.name} (ناموجود)`}
            </option>
          ))}
        </SelectField>
      </div>
      <div className="w-48">
        <Controller
          control={form.control}
          name="price"
          render={({ field }) => (
            <MoneyField
              label="قیمت (تومان)"
              placeholder="۲۵٬۰۰۰"
              error={errors.price?.message}
              name={field.name}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
            />
          )}
        />
      </div>
      <div className="flex w-full gap-2">
        <Button type="submit" size="sm" disabled={isSubmitting}>
          {submitLabel}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </form>
  );
}
