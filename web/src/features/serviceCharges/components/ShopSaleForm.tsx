import { ChevronDown, ChevronUp, Plus, Trash2 } from "lucide-react";
import { useId } from "react";
import { Controller, useFieldArray, useForm, useWatch, type UseFormReturn } from "react-hook-form";

import { FormField, MoneyField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { addMoney, multiplyMoney, normalizeMoney } from "@/lib/money";

import { useRecordShopSale, type ServiceCharge } from "../api";
import {
  emptyShopItem,
  emptyShopSaleValues,
  parseSaleQuantity,
  serviceChargeAmountProblem,
  shopSaleLimits,
  shopSaleSchema,
  stepSaleQuantity,
  type ShopSaleValues,
} from "../schemas";

interface ShopSaleFormProps {
  attendanceId: string;
  /** Called with every item the server saved, in the order they were typed. */
  onDone: (sales: ServiceCharge[]) => void;
  onCancel: () => void;
}

/**
 * «فروشگاه» (BUSINESS_RULES.md §7 *Sale at the desk*): one or more things the desk sells that have
 * no product of their own. Each line is a name, how many (typed, or stepped with ▲/▼) and the price
 * of one; «افزودن کالای دیگر» adds a line, for when two things were sold together.
 *
 * There is no payment here (decided with the developer, 1405/07/12): the whole sale goes on the
 * member's account, like a cafe purchase from the locker, and is paid afterwards from the tile's
 * list or in «تسویه یکجا». So no money moves and there is nothing to confirm.
 */
export function ShopSaleForm({ attendanceId, onDone, onCancel }: ShopSaleFormProps) {
  const recordSale = useRecordShopSale();

  const form = useForm<ShopSaleValues>({
    resolver: zodResolver(shopSaleSchema),
    defaultValues: emptyShopSaleValues,
  });
  const lines = useFieldArray({ control: form.control, name: "items" });

  const items = useWatch({ control: form.control, name: "items" }) ?? [];
  const lineTotals = items.map((item) => lineTotal(item?.quantity ?? "", item?.unitPrice ?? ""));
  const total = lineTotals.every((line) => line !== "") ? addMoney(...lineTotals) : "";

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      const saved = await recordSale.mutateAsync({
        attendanceId,
        items: values.items.map((item) => ({
          description: item.description,
          // The schema has already refused anything that is not a whole number from 1 to 999.
          quantity: parseSaleQuantity(item.quantity)!,
          unitPrice: normalizeMoney(item.unitPrice),
        })),
      });
      onDone(saved);
    } catch (problem) {
      // The form checks every field the API does, so what is left is not about one line.
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors } = form.formState;

  return (
    <form className="space-y-3" onSubmit={onSubmit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <ul className="space-y-3">
        {lines.fields.map((line, index) => (
          <li key={line.id}>
            <fieldset
              className="space-y-3 rounded-md border p-3"
              aria-label={`کالای ${toPersianDigits(index + 1)}`}
            >
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-muted-foreground">
                  کالای {toPersianDigits(index + 1)}
                </span>
                {lines.fields.length > 1 && (
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    className="size-8 has-[>svg]:px-0"
                    aria-label={`حذف کالای ${toPersianDigits(index + 1)}`}
                    onClick={() => lines.remove(index)}
                  >
                    <Trash2 aria-hidden />
                  </Button>
                )}
              </div>

              <FormField
                label="نام کالا"
                autoComplete="off"
                maxLength={shopSaleLimits.descriptionMaxLength}
                error={errors.items?.[index]?.description?.message}
                {...form.register(`items.${index}.description`)}
              />

              <div className="grid gap-3 sm:grid-cols-[10rem_1fr]">
                <QuantityStepper form={form} index={index} />
                <Controller
                  control={form.control}
                  name={`items.${index}.unitPrice`}
                  render={({ field }) => (
                    <MoneyField
                      label="قیمت واحد (تومان)"
                      placeholder="۱۰۰٬۰۰۰"
                      error={errors.items?.[index]?.unitPrice?.message}
                      name={field.name}
                      value={field.value}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                    />
                  )}
                />
              </div>

              {lines.fields.length > 1 && (
                <p className="text-sm text-muted-foreground">
                  جمع این کالا: {lineTotals[index] === "" ? "—" : formatMoney(lineTotals[index]!)}
                </p>
              )}
            </fieldset>
          </li>
        ))}
      </ul>

      {lines.fields.length < shopSaleLimits.maxItems && (
        <Button type="button" variant="outline" size="sm" onClick={() => lines.append(emptyShopItem)}>
          <Plus aria-hidden />
          افزودن کالای دیگر
        </Button>
      )}

      <div className="flex items-center justify-between rounded-md border px-3 py-2 font-bold">
        <span>جمع</span>
        <span>{total === "" ? "—" : formatMoney(total)}</span>
      </div>
      <p className="text-sm text-muted-foreground">
        مبلغ به حساب عضو ثبت می‌شود و بعداً پرداخت می‌شود.
      </p>

      <div className="flex gap-2">
        <Button type="submit" disabled={recordSale.isPending}>
          {recordSale.isPending ? "در حال ثبت…" : "ثبت فروش"}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          انصراف
        </Button>
      </div>
    </form>
  );
}

/** One line's total, or "" while its quantity or price is not a valid figure yet. */
function lineTotal(quantityText: string, unitPriceText: string): string {
  const quantity = parseSaleQuantity(quantityText);
  if (quantity === null || serviceChargeAmountProblem(unitPriceText) !== null) {
    return "";
  }

  return multiplyMoney(normalizeMoney(unitPriceText), quantity);
}

/**
 * How many, typed (Persian or English digits) or stepped with ▲ one more and ▼ one fewer. The
 * buttons write the same text the box holds, so the schema checks both the same way.
 */
function QuantityStepper({ form, index }: { form: UseFormReturn<ShopSaleValues>; index: number }) {
  const inputId = useId();
  const errorId = `${inputId}-error`;
  const name = `items.${index}.quantity` as const;
  const error = form.formState.errors.items?.[index]?.quantity?.message;

  function step(by: 1 | -1) {
    form.setValue(name, stepSaleQuantity(form.getValues(name), by), {
      shouldDirty: true,
      shouldValidate: form.formState.isSubmitted,
    });
  }

  return (
    <div className="space-y-2">
      <Label htmlFor={inputId}>تعداد</Label>
      <div className="flex items-stretch gap-1">
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="size-9 has-[>svg]:px-0"
          aria-label="یکی بیشتر"
          onClick={() => step(1)}
        >
          <ChevronUp aria-hidden />
        </Button>
        <Input
          id={inputId}
          dir="ltr"
          inputMode="numeric"
          autoComplete="off"
          className="text-center"
          aria-invalid={error !== undefined}
          aria-describedby={error !== undefined ? errorId : undefined}
          {...form.register(name)}
        />
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="size-9 has-[>svg]:px-0"
          aria-label="یکی کمتر"
          onClick={() => step(-1)}
        >
          <ChevronDown aria-hidden />
        </Button>
      </div>
      {error !== undefined && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
