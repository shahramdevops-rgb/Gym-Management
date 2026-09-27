import { useState } from "react";
import { Controller, useForm } from "react-hook-form";

import { PageMessage } from "@/features/auth/components/PageMessage";
import { MoneyField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeMoney } from "@/lib/money";

import { usePrices, useUpdatePrices, type Prices } from "../api";
import { pricesSchema, type PricesValues } from "../schemas";

const changedConcurrently = "Pricing.ChangedConcurrently";

function isCode(problem: unknown, code: string): boolean {
  return (
    typeof problem === "object" && problem !== null && "code" in problem && problem.code === code
  );
}

/** The form's text for a price as the API sent it; empty while the Owner has not set it. */
function priceText(price: Prices["sessionPrice"]): string {
  return price === null || price === undefined ? "" : String(price);
}

/**
 * Owner only (the route is wrapped in RequireRole). The gym's two prices (BUSINESS_RULES.md §3
 * *Prices*): one session of a plan, and one single-session visit. Every sale reads them, so the desk
 * never types a price, and a change here reaches only the sales made after it.
 *
 * Like every edit form, it is filled from one `version` and sends it back; if someone saved
 * meanwhile, the page says so and offers to load their prices.
 */
export function SettingsPage() {
  const prices = usePrices();
  const [stale, setStale] = useState(false);
  const [reloading, setReloading] = useState(false);
  // Held here, not in the form: a save changes the version, which rebuilds the form below, and the
  // "saved" line has to outlive that rebuild to be seen at all.
  const [saved, setSaved] = useState(false);

  if (prices.isPending) {
    return <PageMessage>در حال بارگذاری…</PageMessage>;
  }

  if (prices.isError) {
    return <PageMessage>{errorMessage(prices.error)}</PageMessage>;
  }

  async function reload() {
    setReloading(true);
    await prices.refetch();
    setReloading(false);
    setStale(false);
  }

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>تنظیمات قیمت</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">
          قیمت هر پلن برابر است با تعداد جلسات ضرب در «قیمت هر جلسه»؛ تعداد روزها روی قیمت اثری
          ندارد. تغییر قیمت‌ها فقط روی فروش‌های بعدی اثر دارد و اشتراک‌هایی که قبلاً فروخته شده‌اند
          همان قیمت خودشان را نگه می‌دارند.
        </p>

        {stale && (
          <Alert
            variant="destructive"
            className="flex flex-wrap items-center justify-between gap-3"
          >
            <span>{errorMessage({ code: changedConcurrently })}</span>
            <Button size="sm" variant="outline" disabled={reloading} onClick={() => void reload()}>
              بارگذاری اطلاعات تازه
            </Button>
          </Alert>
        )}

        {saved && (
          <Alert variant="success" role="status">
            قیمت‌ها ذخیره شد.
          </Alert>
        )}

        {/* Keyed by version, so fresh prices rebuild the form with their values. */}
        <PricesForm
          key={String(prices.data.version)}
          current={prices.data}
          onSaving={() => setSaved(false)}
          onSaved={() => setSaved(true)}
          onStale={() => setStale(true)}
        />
      </CardContent>
    </Card>
  );
}

interface PricesFormProps {
  current: Prices;
  onSaving: () => void;
  onSaved: () => void;
  onStale: () => void;
}

function PricesForm({ current, onSaving, onSaved, onStale }: PricesFormProps) {
  const updatePrices = useUpdatePrices();
  const form = useForm<PricesValues>({
    resolver: zodResolver(pricesSchema),
    defaultValues: {
      sessionPrice: priceText(current.sessionPrice),
      singleVisitPrice: priceText(current.singleVisitPrice),
    },
  });

  const submit = form.handleSubmit(async (values) => {
    onSaving();
    try {
      await updatePrices.mutateAsync({
        sessionPrice: normalizeMoney(values.sessionPrice),
        singleVisitPrice: normalizeMoney(values.singleVisitPrice),
        version: current.version,
      });
      onSaved();
    } catch (problem) {
      if (isCode(problem, changedConcurrently)) {
        onStale();
        return;
      }
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors, isSubmitting } = form.formState;
  const neverSet = current.sessionPrice === null || current.singleVisitPrice === null;

  return (
    <form className="grid max-w-xl gap-4" onSubmit={submit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}
      {neverSet && (
        <Alert>
          تا وقتی هر دو قیمت وارد نشده‌اند، پذیرش نمی‌تواند پلن یا ورود تک‌جلسه‌ای بفروشد.
        </Alert>
      )}

      <Controller
        control={form.control}
        name="sessionPrice"
        render={({ field }) => (
          <MoneyField
            label="قیمت هر جلسه برای پلن‌ها (تومان)"
            placeholder="۱۰۰٬۰۰۰"
            error={errors.sessionPrice?.message}
            name={field.name}
            value={field.value}
            onChange={field.onChange}
            onBlur={field.onBlur}
          />
        )}
      />

      <Controller
        control={form.control}
        name="singleVisitPrice"
        render={({ field }) => (
          <MoneyField
            label="قیمت تک‌جلسهٔ آزاد (تومان)"
            placeholder="۱۵۰٬۰۰۰"
            error={errors.singleVisitPrice?.message}
            name={field.name}
            value={field.value}
            onChange={field.onChange}
            onBlur={field.onBlur}
          />
        )}
      />

      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "در حال ذخیره…" : "ذخیره"}
        </Button>
      </div>
    </form>
  );
}
