import { useState, type ReactNode } from "react";
import { Controller, useForm, useWatch, type UseFormReturn } from "react-hook-form";

import { PageMessage } from "@/features/auth/components/PageMessage";
import { FormField, SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useSmsCredit, useSmsSettings, useUpdateSmsSettings, type SmsSettings } from "../api";
import {
  isKindFilled,
  lastSendHour,
  sendHours,
  sendMinutes,
  smsSettingsSchema,
  toFormValues,
  toInput,
  type SmsKind,
  type SmsSettingsValues,
} from "../schemas";

const changedConcurrently = "Sms.ChangedConcurrently";

/** The Owner's number follows the members' phone rules, so the server answers with their codes. */
const phoneCodeFields = {
  "Members.PhoneInvalid": "ownerPhone",
  "Members.PhoneNotMobile": "ownerPhone",
  "Members.PhoneNotIranian": "ownerPhone",
} as const;

function isCode(problem: unknown, code: string): boolean {
  return (
    typeof problem === "object" && problem !== null && "code" in problem && problem.code === code
  );
}

interface KindText {
  title: string;
  description: string;
  thresholdLabel: string;
  templatePlaceholder: string;
  hint?: string;
}

/** Each kind's words on the page (BUSINESS_RULES.md §10 *The four kinds*; templates in docs/sms-templates.md). */
const kindText: Record<SmsKind, KindText> = {
  subscriptionExpiring: {
    title: "پایان اشتراک",
    description:
      "به عضو، وقتی تا پایان اشتراک فعالش این تعداد روز یا کمتر مانده باشد. عضوی که اشتراک بعدی را خریده پیامک نمی‌گیرد.",
    thresholdLabel: "چند روز قبل از پایان (۱ تا ۳۰)",
    templatePlaceholder: "gymExpiring",
  },
  lowSessions: {
    title: "جلسات رو به اتمام",
    description:
      "به عضو، وقتی جلسات باقی‌ماندهٔ اشتراک فعالش به این تعداد یا کمتر برسد. عضوی که اشتراک بعدی را خریده پیامک نمی‌گیرد.",
    thresholdLabel: "تعداد جلسهٔ باقی‌مانده (۱ تا ۱۰)",
    templatePlaceholder: "gymLowSessions",
  },
  birthday: {
    title: "تولد",
    description: "به هر عضوی که تاریخ تولد دارد، حتی عضو غیرفعال؛ سالی یک بار، به تاریخ شمسی.",
    thresholdLabel: "چند روز قبل از تولد (۰ تا ۷؛ ۰ یعنی خود روز)",
    templatePlaceholder: "gymBirthday",
    hint: "برای خود روز تولد قالب gymBirthday و برای تبریک پیشاپیش (۱ تا ۷ روز قبل) قالب gymBirthdayEarly را بنویسید.",
  },
  payableDue: {
    title: "چک و قسط",
    description: "به شما (مدیر)، برای هر چک و هر قسطِ در انتظار، یک پیامک جدا.",
    thresholdLabel: "چند روز قبل از سررسید (۰ تا ۳۰؛ ۰ یعنی خود روز)",
    templatePlaceholder: "gymPayableDue",
  },
};

/**
 * Owner only (the route is wrapped in RequireRole). «تنظیمات پیامک» (BUSINESS_RULES.md §10 *SMS
 * settings*): which SMS the gym sends, when, and with which Kavenegar template. It starts empty and
 * everything off; a kind can be turned on only once its fields are filled, because every SMS costs
 * money and nothing is sent that the Owner did not choose.
 *
 * Like every edit form, it is filled from one `version` and sends it back; if someone saved
 * meanwhile, the page says so and offers to load their settings.
 */
export function SmsSettingsPage() {
  const settings = useSmsSettings();
  const [stale, setStale] = useState(false);
  const [reloading, setReloading] = useState(false);
  // Held here, not in the form: a save changes the version, which rebuilds the form below.
  const [notice, setNotice] = useState<SaveNotice>(null);

  if (settings.isPending) {
    return <PageMessage>در حال بارگذاری…</PageMessage>;
  }

  if (settings.isError) {
    return <PageMessage>{errorMessage(settings.error)}</PageMessage>;
  }

  async function reload() {
    setReloading(true);
    await settings.refetch();
    setReloading(false);
    setStale(false);
  }

  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle>تنظیمات پیامک</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">
          هر پیامک هزینه دارد. تا وقتی شما نوعی را روشن نکنید، هیچ پیامکی فرستاده نمی‌شود. تغییرها
          از اجرای بعدی اعمال می‌شوند و به کسی که پیامکش را گرفته دوباره فرستاده نمی‌شود. متن
          پیامک‌ها در پنل کاوه‌نگار نوشته و تأیید می‌شود؛ اینجا فقط نام قالب آن وارد می‌شود.
        </p>

        <SmsCreditLine />

        {/* Keyed by version, so fresh settings rebuild the form with their values. */}
        <SmsSettingsForm
          key={String(settings.data.version)}
          current={settings.data}
          onNotice={setNotice}
          onStale={() => setStale(true)}
          status={
            stale ? (
              <Alert
                variant="destructive"
                className="flex flex-wrap items-center justify-between gap-3"
              >
                <span>{errorMessage({ code: changedConcurrently })}</span>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={reloading}
                  onClick={() => void reload()}
                >
                  بارگذاری اطلاعات تازه
                </Button>
              </Alert>
            ) : notice === "saved" ? (
              <Alert variant="success" role="status">
                تغییرها ثبت شد.
              </Alert>
            ) : notice === "unchanged" ? (
              <Alert role="status">تغییری نداده‌اید؛ چیزی ذخیره نشد.</Alert>
            ) : null
          }
        />
      </CardContent>
    </Card>
  );
}

/** What the last press of «ذخیره» did, shown beside the button where the Owner is looking. */
type SaveNotice = "saved" | "unchanged" | null;

/**
 * The Kavenegar account's remaining credit (BUSINESS_RULES.md §10 *Sending*). In test mode
 * (`Sms:Provider` is `Fake`) nothing is really sent, and the page says so instead.
 */
function SmsCreditLine() {
  const credit = useSmsCredit();

  if (credit.isPending) {
    return null;
  }

  if (credit.isError || (!credit.data.isTestMode && credit.data.remainingToman === null)) {
    return <p className="text-sm text-muted-foreground">اعتبار پنل پیامک الان در دسترس نیست.</p>;
  }

  if (credit.data.isTestMode) {
    return (
      <Alert role="status">حالت آزمایشی: پیامکی واقعاً فرستاده نمی‌شود و فقط ثبت می‌شود.</Alert>
    );
  }

  return (
    <p className="text-sm">
      اعتبار باقی‌ماندهٔ پنل پیامک:{" "}
      <span className="font-medium">{formatMoney(credit.data.remainingToman)}</span>
    </p>
  );
}

interface SmsSettingsFormProps {
  current: SmsSettings;
  onNotice: (notice: SaveNotice) => void;
  onStale: () => void;
  /** The outcome of the last save, shown beside the button: the page may be scrolled far from its top. */
  status: ReactNode;
}

/**
 * «ذخیره» is always enabled. Pressed with nothing changed, it sends nothing and says so; "nothing
 * changed" means the request would carry exactly what is saved, so typing a value and putting it
 * back counts as no change. Any edit clears the last message, so «ثبت شد» never sits beside fields
 * that were changed after it.
 */
function SmsSettingsForm({ current, onNotice, onStale, status }: SmsSettingsFormProps) {
  const updateSettings = useUpdateSmsSettings();
  const [savedInput] = useState(() =>
    JSON.stringify(toInput(toFormValues(current), current.version)),
  );
  const form = useForm<SmsSettingsValues>({
    resolver: zodResolver(smsSettingsSchema),
    defaultValues: toFormValues(current),
  });

  const submit = form.handleSubmit(async (values) => {
    const input = toInput(values, current.version);
    if (JSON.stringify(input) === savedInput) {
      onNotice("unchanged");
      return;
    }

    onNotice(null);
    try {
      await updateSettings.mutateAsync(input);
      onNotice("saved");
    } catch (problem) {
      if (isCode(problem, changedConcurrently)) {
        onStale();
        return;
      }
      applyServerErrors(problem, form.setError, phoneCodeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    // Every field is a native input, select or checkbox, and their change events bubble up to here.
    <form className="grid gap-4" onSubmit={submit} onChange={() => onNotice(null)} noValidate>
      <Controller
        control={form.control}
        name="enabled"
        render={({ field }) => (
          <div className="space-y-1 rounded-md border p-4">
            <label className="flex w-fit cursor-pointer items-center gap-2 font-medium">
              <input
                type="checkbox"
                className="size-4 accent-primary"
                name={field.name}
                checked={field.value}
                onChange={(event) => field.onChange(event.target.checked)}
                onBlur={field.onBlur}
              />
              ارسال پیامک روشن باشد
            </label>
            <p className="text-sm text-muted-foreground">
              وقتی خاموش است هیچ پیامکی فرستاده نمی‌شود، حتی اگر نوعی در پایین روشن باشد.
            </p>
          </div>
        )}
      />

      <KindFieldset kind="subscriptionExpiring" form={form} />
      <KindFieldset kind="lowSessions" form={form} />
      <KindFieldset kind="birthday" form={form} />
      <KindFieldset kind="payableDue" form={form}>
        <FormField
          label="شماره موبایل مدیر"
          placeholder="۰۹۱۲ ۱۲۳ ۴۵۶۷"
          inputMode="tel"
          dir="ltr"
          className="text-end"
          error={errors.ownerPhone?.message}
          {...form.register("ownerPhone")}
        />
      </KindFieldset>

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "در حال ذخیره…" : "ذخیره"}
        </Button>
        {errors.root?.server !== undefined ? (
          <Alert variant="destructive" className="w-auto flex-1">
            {errors.root.server.message}
          </Alert>
        ) : (
          status !== null && <div className="flex-1 [&>[data-slot=alert]]:py-2">{status}</div>
        )}
      </div>
    </form>
  );
}

interface KindFieldsetProps {
  kind: SmsKind;
  form: UseFormReturn<SmsSettingsValues>;
  /** Fields only this kind has (the Owner's number for cheques and instalments). */
  children?: ReactNode;
}

/**
 * One kind: its switch, number, send time and template name. The switch cannot be turned on while
 * a field is empty; one that is already on can always be turned off, even after a field is emptied
 * (the save then says what is missing).
 */
function KindFieldset({ kind, form, children }: KindFieldsetProps) {
  const control = form.control;
  const text = kindText[kind];
  const values = useWatch({ control, name: kind });
  const ownerPhone = useWatch({ control, name: "ownerPhone" });
  const filled = isKindFilled(kind, values, ownerPhone);
  const errors = form.formState.errors[kind];

  return (
    <fieldset className="space-y-3 rounded-md border p-4">
      <legend className="px-1 font-medium">{text.title}</legend>
      <p className="text-sm text-muted-foreground">{text.description}</p>

      <Controller
        control={control}
        name={`${kind}.enabled`}
        render={({ field }) => (
          <div className="space-y-1">
            <label className="flex w-fit cursor-pointer items-center gap-2 text-sm has-disabled:cursor-not-allowed has-disabled:opacity-60">
              <input
                type="checkbox"
                className="size-4 accent-primary"
                name={field.name}
                checked={field.value}
                disabled={!field.value && !filled}
                aria-invalid={errors?.enabled !== undefined}
                onChange={(event) => field.onChange(event.target.checked)}
                onBlur={field.onBlur}
              />
              روشن
            </label>
            {!field.value && !filled && (
              <p className="text-sm text-muted-foreground">
                برای روشن کردن، اول همهٔ خانه‌های این بخش را پر کنید.
              </p>
            )}
            {errors?.enabled?.message !== undefined && (
              <p className="text-sm text-destructive">{errors.enabled.message}</p>
            )}
          </div>
        )}
      />

      <div className="grid gap-3 sm:grid-cols-2">
        <FormField
          label={text.thresholdLabel}
          inputMode="numeric"
          error={errors?.threshold?.message}
          {...form.register(`${kind}.threshold`)}
        />

        <Controller
          control={control}
          name={`${kind}.sendTime`}
          render={({ field }) => (
            <SendTimeField
              value={field.value}
              onChange={field.onChange}
              error={errors?.sendTime?.message}
            />
          )}
        />

        <FormField
          label="نام قالب در کاوه‌نگار"
          placeholder={text.templatePlaceholder}
          dir="ltr"
          className="text-end"
          autoComplete="off"
          error={errors?.templateName?.message}
          {...form.register(`${kind}.templateName`)}
        />

        {children}
      </div>

      {text.hint !== undefined && <p className="text-sm text-muted-foreground">{text.hint}</p>}
    </fieldset>
  );
}

interface SendTimeFieldProps {
  /** `HH:mm`, or empty. */
  value: string;
  onChange: (value: string) => void;
  error?: string;
}

/**
 * An hour from ۸ to ۲۲ and a minute on the quarter (decided with the developer, task 10.2). The
 * minute box follows the hour: choosing an hour fills in :00, and ۲۲ allows only :00, since 22:00 is
 * the last send time. So the two boxes can only make a time the server accepts.
 */
function SendTimeField({ value, onChange, error }: SendTimeFieldProps) {
  const [hour = "", minute = ""] = value === "" ? [] : value.split(":");
  const minutes = hour === lastSendHour ? ["00"] : sendMinutes;

  function chooseHour(next: string) {
    if (next === "") {
      onChange("");
      return;
    }
    const keptMinute = next === lastSendHour || minute === "" ? "00" : minute;
    onChange(`${next}:${keptMinute}`);
  }

  return (
    <div className="grid grid-cols-2 gap-2">
      <SelectField
        label="ساعت ارسال"
        value={hour}
        error={error}
        onChange={(event) => chooseHour(event.target.value)}
      >
        <option value="">—</option>
        {sendHours.map((option) => (
          <option key={option} value={option}>
            {toPersianDigits(option)}
          </option>
        ))}
      </SelectField>
      <SelectField
        label="دقیقه"
        value={minute}
        disabled={hour === ""}
        onChange={(event) => onChange(`${hour}:${event.target.value}`)}
      >
        {hour === "" && <option value="">—</option>}
        {minutes.map((option) => (
          <option key={option} value={option}>
            {toPersianDigits(option)}
          </option>
        ))}
      </SelectField>
    </div>
  );
}
