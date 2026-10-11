import { useState, type ReactNode } from "react";
import { Controller, useForm } from "react-hook-form";

import { LoadingState } from "@/components/States";
import { PageHeader } from "@/components/PageHeader";
import { MoneyField } from "@/components/FormField";
import { SaveRow, StaleAlert, type SaveNotice } from "@/components/SaveRow";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { applyServerErrors, useUnchanged, zodResolver } from "@/lib/forms";
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
  const [notice, setNotice] = useState<SaveNotice>(null);

  if (prices.isPending) {
    return <LoadingState />;
  }

  if (prices.isError) {
    return <Alert variant="destructive">{errorMessage(prices.error)}</Alert>;
  }

  async function reload() {
    setReloading(true);
    await prices.refetch();
    setReloading(false);
    setStale(false);
  }

  return (
    <div className="max-w-2xl space-y-6">
      <PageHeader title="تنظیمات قیمت" />
      <Card>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">
            قیمت هر پلن برابر است با تعداد جلسات ضرب در «قیمت هر جلسه»؛ تعداد روزها روی قیمت اثری
            ندارد. تغییر قیمت‌ها فقط روی فروش‌های بعدی اثر دارد و اشتراک‌هایی که قبلاً فروخته
            شده‌اند همان قیمت خودشان را نگه می‌دارند.
          </p>

          {/* Keyed by version, so fresh prices rebuild the form with their values. */}
          <PricesForm
            key={String(prices.data.version)}
            current={prices.data}
            notice={notice}
            onNotice={setNotice}
            onStale={() => setStale(true)}
            stale={
              stale ? (
                <StaleAlert
                  code={changedConcurrently}
                  reloading={reloading}
                  onReload={() => void reload()}
                />
              ) : undefined
            }
          />
        </CardContent>
      </Card>
    </div>
  );
}

interface PricesFormProps {
  current: Prices;
  notice: SaveNotice;
  onNotice: (notice: SaveNotice) => void;
  onStale: () => void;
  stale: ReactNode;
}

/** The request these values make: what a save sends, and what "nothing changed" compares. */
function toInput(values: PricesValues, version: Prices["version"]) {
  return {
    sessionPrice: normalizeMoney(values.sessionPrice),
    singleVisitPrice: normalizeMoney(values.singleVisitPrice),
    version,
  };
}

/**
 * «ذخیره» is always enabled; pressed with the prices as they were, it sends nothing and says so
 * beside the button, where every outcome of a save is shown (roadmap 13.2c). Any edit clears it.
 */
function PricesForm({ current, notice, onNotice, onStale, stale }: PricesFormProps) {
  const updatePrices = useUpdatePrices();
  const defaultValues: PricesValues = {
    sessionPrice: priceText(current.sessionPrice),
    singleVisitPrice: priceText(current.singleVisitPrice),
  };
  const isUnchanged = useUnchanged(() => toInput(defaultValues, current.version));
  const form = useForm<PricesValues>({ resolver: zodResolver(pricesSchema), defaultValues });

  const submit = form.handleSubmit(async (values) => {
    const input = toInput(values, current.version);
    if (isUnchanged(input)) {
      onNotice("unchanged");
      return;
    }

    onNotice(null);
    try {
      await updatePrices.mutateAsync(input);
      onNotice("saved");
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
    // Both boxes are native inputs, and their change events bubble up to here.
    <form
      className="grid max-w-xl gap-4"
      onSubmit={submit}
      onChange={() => onNotice(null)}
      noValidate
    >
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

      <SaveRow error={errors.root?.server?.message} notice={notice} stale={stale}>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "در حال ذخیره…" : "ذخیره"}
        </Button>
      </SaveRow>
    </form>
  );
}
