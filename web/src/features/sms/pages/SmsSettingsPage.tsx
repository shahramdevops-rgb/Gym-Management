import { useState, type ReactNode } from "react";
import { Controller, useForm, useWatch, type UseFormReturn } from "react-hook-form";

import { PageMessage } from "@/features/auth/components/PageMessage";
import { FormField, MoneyField, SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useSmsSettings, useUpdateSmsSettings, type SmsSettings } from "../api";
import { SmsCreditLine } from "../components/SmsCreditLine";
import {
  isDebtReminderFilled,
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

/**
 * Refusals that come back as one code rather than per field. The Owner's number follows the
 * members' phone rules, so the server answers with their codes.
 */
const codeFields = {
  "Members.PhoneInvalid": "ownerPhone",
  "Members.PhoneNotMobile": "ownerPhone",
  "Members.PhoneNotIranian": "ownerPhone",
  "Sms.DailyReportPhoneInvalid": "dailyReport.phone",
  "Sms.DebtReminderDaysOutOfRange": "debtReminder.daysAfter",
  "Sms.DebtReminderMinAmountOutOfRange": "debtReminder.minAmount",
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
  hint?: string;
}

/** Each kind's words on the page (BUSINESS_RULES.md §10 *The four kinds*; the texts in docs/sms-texts.md). */
const kindText: Record<SmsKind, KindText> = {
  subscriptionExpiring: {
    title: "پایان اشتراک",
    description:
      "به عضو، وقتی تا پایان اشتراک فعالش این تعداد روز یا کمتر مانده باشد. عضوی که اشتراک بعدی را خریده پیامک نمی‌گیرد.",
    thresholdLabel: "چند روز قبل از پایان (۱ تا ۳۰)",
  },
  lowSessions: {
    title: "جلسات رو به اتمام",
    description:
      "به عضو، وقتی جلسات باقی‌ماندهٔ اشتراک فعالش به این تعداد یا کمتر برسد. عضوی که اشتراک بعدی را خریده پیامک نمی‌گیرد.",
    thresholdLabel: "تعداد جلسهٔ باقی‌مانده (۱ تا ۱۰)",
  },
  birthday: {
    title: "تولد",
    description:
      "به عضو فعالی که پلن دارد (فعال، فریز یا خریده‌شده برای بعد)؛ سالی یک بار، به تاریخ شمسی. ورود روز تولد برای او رایگان است و پیامک همین را خبر می‌دهد.",
    thresholdLabel: "چند روز قبل از تولد (۱ تا ۷)",
    hint: "پیامک هیچ‌وقت در خود روز تولد نمی‌رود، تا عضو برای آمدن برنامه بریزد.",
  },
  payableDue: {
    title: "چک و قسط",
    description: "به شما (مدیر)، برای هر چک و هر قسطِ در انتظار، یک پیامک جدا.",
    thresholdLabel: "چند روز قبل از سررسید (۰ تا ۳۰؛ ۰ یعنی خود روز)",
  },
  absence: {
    title: "پیامک غیبت",
    description:
      "به عضو فعالی که پلن قابل‌استفاده دارد (نه فریز، نه تک‌جلسه) و این تعداد روز نیامده؛ اگر از شروع پلن نیامده، از روز شروع شمرده می‌شود. در هر غیبت یک بار؛ بعد از آمدن دوباره، غیبت بعدی پیامک خودش را دارد.",
    thresholdLabel: "چند روز نیامده (۷ تا ۶۰)",
  },
};

/**
 * Owner only (the route is wrapped in RequireRole). «تنظیمات پیامک» (BUSINESS_RULES.md §10 *SMS
 * settings*): which SMS the gym sends, and when; the texts are fixed in the API. It starts empty and
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
          از اجرای بعدی اعمال می‌شوند و به کسی که پیامکش را گرفته دوباره فرستاده نمی‌شود. پیامک‌ها
          از خط اختصاصی باشگاه فرستاده می‌شوند و متنشان ثابت است.
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
      applyServerErrors(problem, form.setError, codeFields);
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
      <DailyReportFieldset form={form} sendTime={current.dailyReport.sendTime} />
      <SubscriptionReceiptFieldset
        form={form}
        morningSendTime={current.subscriptionReceipt.morningSendTime}
      />
      <WelcomeFieldset form={form} morningSendTime={current.welcome.morningSendTime} />
      <DebtReminderFieldset form={form} />
      <KindFieldset kind="absence" form={form} />
      <BroadcastFieldset form={form} />

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
 * One kind: its switch, number and send time. The switch cannot be turned on while
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

        {children}
      </div>

      {text.hint !== undefined && <p className="text-sm text-muted-foreground">{text.hint}</p>}
    </fieldset>
  );
}

interface DailyReportFieldsetProps {
  form: UseFormReturn<SmsSettingsValues>;
  /** `HH:mm:ss` from the API: fixed in the code, shown so the page says the time the server uses. */
  sendTime: string;
}

/**
 * The Owner's daily report (BUSINESS_RULES.md §10 *The daily report*, task 10.7): a switch and the
 * number it goes to, apart from the cheques' number. No number of days and no time to choose: it
 * goes every night at 22:45 about that same day.
 */
function DailyReportFieldset({ form, sendTime }: DailyReportFieldsetProps) {
  const control = form.control;
  const phone = useWatch({ control, name: "dailyReport.phone" });
  const filled = phone.trim() !== "";
  const errors = form.formState.errors.dailyReport;
  const time = toPersianDigits(sendTime.slice(0, 5));

  return (
    <fieldset className="space-y-3 rounded-md border p-4">
      <legend className="px-1 font-medium">گزارش روزانه</legend>
      <p className="text-sm text-muted-foreground">
        هر شب ساعت {time} برای همان روز: تعداد ورود اعضا، پلن و تک‌جلسهٔ فروخته‌شده، و دریافتی (بدون
        فروشگاه، آنالیز و متفرقه) با نقد، کارت و انتقال، به هزار تومان. روزی که هیچ ورود، فروش و
        پولی نداشته فرستاده نمی‌شود.
      </p>

      <Controller
        control={control}
        name="dailyReport.enabled"
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
                برای روشن کردن، اول شمارهٔ موبایل را بنویسید.
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
          label="شماره موبایل گیرندهٔ گزارش"
          placeholder="۰۹۱۲ ۱۲۳ ۴۵۶۷"
          inputMode="tel"
          dir="ltr"
          className="text-end"
          error={errors?.phone?.message}
          {...form.register("dailyReport.phone")}
        />
      </div>
    </fieldset>
  );
}

interface SubscriptionReceiptFieldsetProps {
  form: UseFormReturn<SmsSettingsValues>;
  /** `HH:mm:ss` from the API: when a receipt for a payment after 22:00 goes, fixed in the code. */
  morningSendTime: string;
}

/**
 * The member's receipt (BUSINESS_RULES.md §10 *The subscription receipt*, task 10.9): only a
 * switch. It goes to the member's own number when a plan is fully paid, so there is nothing to fill in.
 */
function SubscriptionReceiptFieldset({ form, morningSendTime }: SubscriptionReceiptFieldsetProps) {
  const morning = toPersianDigits(morningSendTime.slice(0, 5));

  return (
    <fieldset className="space-y-3 rounded-md border p-4">
      <legend className="px-1 font-medium">رسید خرید اشتراک</legend>
      <p className="text-sm text-muted-foreground">
        وقتی پول یک پلن کامل پرداخت شد، به خود عضو: تعداد جلسه، تاریخ شروع و پایان، و مبلغ. همان
        لحظه فرستاده می‌شود؛ پرداختی که بعد از ساعت ۲۲ ثبت شود، صبح روز بعد ساعت {morning}. تک‌جلسه
        و اشتراک رایگان رسید ندارند.
      </p>

      <label className="flex w-fit cursor-pointer items-center gap-2 text-sm">
        <input
          type="checkbox"
          className="size-4 accent-primary"
          {...form.register("subscriptionReceipt.enabled")}
        />
        روشن
      </label>
    </fieldset>
  );
}

interface WelcomeFieldsetProps {
  form: UseFormReturn<SmsSettingsValues>;
  /** `HH:mm:ss` from the API: when a welcome for a sale after 22:00 goes, fixed in the code. */
  morningSendTime: string;
}

/**
 * The member's welcome (BUSINESS_RULES.md §10 *The welcome*, task 10.10): only a switch. It goes to
 * the member's own number once, when their first plan is sold.
 */
function WelcomeFieldset({ form, morningSendTime }: WelcomeFieldsetProps) {
  const morning = toPersianDigits(morningSendTime.slice(0, 5));

  return (
    <fieldset className="space-y-3 rounded-md border p-4">
      <legend className="px-1 font-medium">خوش‌آمد</legend>
      <p className="text-sm text-muted-foreground">
        یک بار برای هر عضو، وقتی اولین پلنش فروخته شد، پرداخت‌شده یا نسیه: «… عزیز، به باشگاه
        پاسارگاد خوش آمدید!». همان لحظه فرستاده می‌شود؛ فروشی که بعد از ساعت ۲۲ ثبت شود، صبح روز بعد
        ساعت {morning}. عضوی که قبلاً پلن داشته، و تک‌جلسه، خوش‌آمد نمی‌گیرد.
      </p>

      <label className="flex w-fit cursor-pointer items-center gap-2 text-sm">
        <input
          type="checkbox"
          className="size-4 accent-primary"
          {...form.register("welcome.enabled")}
        />
        روشن
      </label>
    </fieldset>
  );
}

interface BroadcastFieldsetProps {
  form: UseFormReturn<SmsSettingsValues>;
}

/**
 * The Owner's broadcast (BUSINESS_RULES.md §10 *The broadcast*, task 10.13): only a switch. The text
 * and who it goes to are chosen each time on «پیامک گروهی».
 */
function BroadcastFieldset({ form }: BroadcastFieldsetProps) {
  return (
    <fieldset className="space-y-3 rounded-md border p-4">
      <legend className="px-1 font-medium">پیامک گروهی</legend>
      <p className="text-sm text-muted-foreground">
        متنی که خودتان در صفحهٔ «پیامک گروهی» می‌نویسید، با نام هر عضو در ابتدا، به همهٔ اعضای فعال
        یا فقط اعضای دارای پلن. همان لحظه و فقط بین ساعت ۸ تا ۲۲ فرستاده می‌شود. تا این روشن نباشد،
        آن صفحه چیزی نمی‌فرستد.
      </p>

      <label className="flex w-fit cursor-pointer items-center gap-2 text-sm">
        <input
          type="checkbox"
          className="size-4 accent-primary"
          {...form.register("broadcast.enabled")}
        />
        روشن
      </label>
    </fieldset>
  );
}

interface DebtReminderFieldsetProps {
  form: UseFormReturn<SmsSettingsValues>;
}

/**
 * The member's debt reminder (BUSINESS_RULES.md §10 *The debt reminder*, task 10.11): a daily run
 * like the four kinds, with a minimum amount besides the days and the time. The switch follows the
 * same rule as theirs: it cannot be turned on while a field is empty.
 */
function DebtReminderFieldset({ form }: DebtReminderFieldsetProps) {
  const control = form.control;
  const values = useWatch({ control, name: "debtReminder" });
  const filled = isDebtReminderFilled(values);
  const errors = form.formState.errors.debtReminder;

  return (
    <fieldset className="space-y-3 rounded-md border p-4">
      <legend className="px-1 font-medium">یادآوری بدهی</legend>
      <p className="text-sm text-muted-foreground">
        به عضوی که قدیمی‌ترین بدهی پرداخت‌نشده‌اش این تعداد روز یا بیشتر مانده و کل بدهی‌اش دست‌کم
        این مبلغ است، با کل مانده‌اش: «… عزیز، مانده حساب شما در باشگاه پاسارگاد … تومان است. لطفاً
        در مراجعه بعدی تسویه بفرمایید.». برای هر بدهی یک بار؛ پیامک بعدی فقط وقتی می‌رود که همهٔ
        بدهی آن روز پرداخت شده باشد و بدهی تازه‌ای این‌قدر بماند. عضو غیرفعال هم می‌گیرد.
      </p>

      <Controller
        control={control}
        name="debtReminder.enabled"
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
          label="چند روز پس از قدیمی‌ترین بدهی (۱ تا ۹۰)"
          inputMode="numeric"
          error={errors?.daysAfter?.message}
          {...form.register("debtReminder.daysAfter")}
        />

        <Controller
          control={control}
          name="debtReminder.sendTime"
          render={({ field }) => (
            <SendTimeField
              value={field.value}
              onChange={field.onChange}
              error={errors?.sendTime?.message}
            />
          )}
        />

        <Controller
          control={control}
          name="debtReminder.minAmount"
          render={({ field }) => (
            <MoneyField
              label="حداقل کل بدهی (تومان، از ۱٬۰۰۰)"
              placeholder="۲۰۰٬۰۰۰"
              error={errors?.minAmount?.message}
              name={field.name}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
            />
          )}
        />
      </div>
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
