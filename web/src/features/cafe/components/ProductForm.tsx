import { useState } from "react";
import { Controller, useForm } from "react-hook-form";

import { FormField, MoneyField, SelectField } from "@/components/FormField";
import { SaveRow, type SaveNotice } from "@/components/SaveRow";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { toneButton } from "@/lib/edges";
import { applyServerErrors, useUnchanged, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

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
  /**
   * Editing a product rather than adding one (roadmap 13.2c): «ذخیره» with nothing changed sends
   * nothing and says so, and every outcome is shown beside the button.
   */
  editing?: boolean;
}

/** The values as a save sends them: the price as the API reads it, whichever digits were typed. */
function comparable(values: ProductValues) {
  return { name: values.name, categoryId: values.categoryId, price: normalizeMoney(values.price) };
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
  editing = false,
}: ProductFormProps) {
  const form = useForm({ resolver: zodResolver(productSchema), defaultValues });
  const isUnchanged = useUnchanged(() => comparable(defaultValues));
  const [notice, setNotice] = useState<SaveNotice>(null);

  const submit = form.handleSubmit(async (values) => {
    if (editing && isUnchanged(comparable(values))) {
      setNotice("unchanged");
      return;
    }

    setNotice(null);
    try {
      await onSubmit(values);
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    // Every field is a native input or select, and their change events bubble up to here.
    <form
      className="flex flex-wrap items-start gap-3"
      onSubmit={submit}
      onChange={() => setNotice(null)}
      noValidate
    >
      {!editing && errors.root?.server !== undefined && (
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
      <div className="w-full">
        <SaveRow error={editing ? errors.root?.server?.message : undefined} notice={notice}>
          <Button
            type="submit"
            size="sm"
            variant="ghost"
            className={toneButton}
            disabled={isSubmitting}
          >
            {submitLabel}
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
            انصراف
          </Button>
        </SaveRow>
      </div>
    </form>
  );
}
