import { useEffect, useId, useRef, useState, type ComponentProps, type ReactNode } from "react";
import { Controller, useForm, useWatch, type UseFormReturn } from "react-hook-form";
import { useLocation } from "react-router";

import { PageMessage } from "@/features/auth/components/PageMessage";
import { FormField, MoneyField, SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeDigits } from "@/lib/normalize";
import { cn } from "@/lib/utils";

import {
  useSmsSamples,
  useSmsSettings,
  useUpdateSmsSettings,
  type SmsSamples,
  type SmsSettings,
} from "../api";
import { BroadcastForm, type BroadcastNotice } from "../components/BroadcastForm";
import { LatestBroadcast } from "../components/LatestBroadcast";
import { SmsCreditLine } from "../components/SmsCreditLine";
import { SmsPreview } from "../components/SmsPreview";
import {
  isDebtReminderFilled,
  isLowSessionsFilled,
  isKindFilled,
  lastSendHour,
  lowSessionsLimits,
  reportHours,
  sendHours,
  sendMinutes,
  smsSettingsSchema,
  toFormValues,
  toInput,
  type SmsKind,
  type SmsSettingsValues,
} from "../schemas";

const changedConcurrently = "Sms.ChangedConcurrently";

/** A number of days or sessions: two digits under a short label, the range inside the box. */
const numberWidth = "w-40";

/** A mobile number, written in groups: ۰۹۱۲ ۱۲۳ ۴۵۶۷. */
const phoneWidth = "w-48";

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
  "Sms.DailyReportSendTimeOutOfRange": "dailyReport.sendTime",
  "Sms.LowSessionsSmallPlanMaxOutOfRange": "lowSessions.smallPlanMax",
  "Sms.LowSessionsMediumPlanMaxOutOfRange": "lowSessions.mediumPlanMax",
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
  /** The numbers the server accepts, shown inside the empty box. */
  range: string;
  hint?: string;
}

/** Each kind's words on the page (BUSINESS_RULES.md §10 *The four kinds*; the texts in docs/sms-texts.md). */
const kindText: Record<SmsKind, KindText> = {
  subscriptionExpiring: {
    title: "پایان اشتراک",
    description:
      "به عضو، وقتی تا پایان اشتراک فعالش این تعداد روز یا کمتر مانده باشد. عضوی که اشتراک بعدی را خریده پیامک نمی‌گیرد.",
    thresholdLabel: "چند روز قبل از پایان",
    range: "۱ تا ۳۰",
  },
  birthday: {
    title: "تولد",
    description:
      "به عضو فعالی که پلن دارد (فعال، فریز یا خریده‌شده برای بعد)؛ سالی یک بار، به تاریخ شمسی. ورود روز تولد برای او رایگان است و پیامک همین را خبر می‌دهد.",
    thresholdLabel: "چند روز قبل از تولد",
    range: "۱ تا ۷",
    hint: "پیامک هیچ‌وقت در خود روز تولد نمی‌رود، تا عضو برای آمدن برنامه بریزد.",
  },
  payableDue: {
    title: "چک و قسط",
    description: "به شما (مدیر)، برای هر چک و هر قسطِ در انتظار، یک پیامک جدا.",
    thresholdLabel: "چند روز قبل از سررسید",
    range: "۰ تا ۳۰",
    hint: "۰ یعنی خود روز سررسید.",
  },
  absence: {
    title: "پیامک غیبت",
    description:
      "به عضو فعالی که پلن قابل‌استفاده دارد (نه فریز، نه تک‌جلسه) و این تعداد روز نیامده؛ اگر از شروع پلن نیامده، از روز شروع شمرده می‌شود. در هر غیبت یک بار؛ بعد از آمدن دوباره، غیبت بعدی پیامک خودش را دارد.",
    thresholdLabel: "چند روز نیامده",
    range: "۷ تا ۶۰",
  },
};

/**
 * Owner only (the route is wrapped in RequireRole). «تنظیمات پیامک» (BUSINESS_RULES.md §10 *SMS
 * settings*): which SMS the gym sends, and when; the texts are fixed in the API. It starts empty and
 * everything off; a kind can be turned on only once its fields are filled, because every SMS costs
 * money and nothing is sent that the Owner did not choose.
 *
 * Each kind is a box with its few settings and, at its foot, its message as the API writes it with a
 * sample member (task 10.15), so the Owner sees exactly what goes out. Writing and sending a
 * broadcast is the page's last section, outside the settings form.
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
  useScrollToBroadcast(settings.isSuccess);

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
    <div className="space-y-6">
      <h2 className="text-xl font-bold">تنظیمات پیامک</h2>

      <div className="space-y-3">
        <p className="max-w-4xl text-sm text-muted-foreground">
          هر پیامک هزینه دارد. تا وقتی شما نوعی را روشن نکنید، هیچ پیامکی فرستاده نمی‌شود. تغییرها
          از اجرای بعدی اعمال می‌شوند و به کسی که پیامکش را گرفته دوباره فرستاده نمی‌شود. پیامک‌ها
          از خط اختصاصی باشگاه فرستاده می‌شوند و متنشان ثابت است؛ کنار هر نوع، همان پیامکی را
          می‌بینید که فرستاده می‌شود.
        </p>

        <SmsCreditLine />
      </div>

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
    </div>
  );
}

/** The broadcast card's id: «پیامک‌ها» links to it (`paths.smsBroadcast`). */
const broadcastCardId = "broadcast";

/**
 * «پیامک‌ها» links to the broadcast card (`#broadcast`); the page draws it only once the settings
 * have loaded, so the browser's own jump to it has already missed it. Done once.
 */
function useScrollToBroadcast(loaded: boolean) {
  const { hash } = useLocation();
  const done = useRef(false);

  useEffect(() => {
    if (!loaded || done.current || hash !== `#${broadcastCardId}`) {
      return;
    }
    done.current = true;
    document.getElementById(broadcastCardId)?.scrollIntoView();
  }, [loaded, hash]);
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
 * «ذخیره» is always enabled, and stays at the bottom of the screen while the page scrolls. Pressed
 * with nothing changed, it sends nothing and says so; "nothing changed" means the request would
 * carry exactly what is saved, so typing a value and putting it back counts as no change. Any edit
 * clears the last message, so «ثبت شد» never sits beside fields that were changed after it.
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
    <form className="space-y-8" onSubmit={submit} onChange={() => onNotice(null)} noValidate>
      <Controller
        control={form.control}
        name="enabled"
        render={({ field }) => (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-card p-5 shadow-sm">
            <div className="space-y-1">
              <p className="font-semibold">همهٔ پیامک‌ها</p>
              <p className="text-sm text-muted-foreground">
                وقتی خاموش است هیچ پیامکی فرستاده نمی‌شود، حتی اگر نوعی در پایین روشن باشد.
              </p>
            </div>
            <OnSwitch
              label="ارسال پیامک روشن باشد"
              name={field.name}
              checked={field.value}
              onChange={(event) => field.onChange(event.target.checked)}
              onBlur={field.onBlur}
            />
          </div>
        )}
      />

      <KindSection title="پیامک به اعضا" description="به شمارهٔ موبایل خود عضو.">
        <KindFieldset kind="subscriptionExpiring" form={form} />
        <LowSessionsFieldset form={form} />
        <KindFieldset kind="birthday" form={form} />
        <SubscriptionReceiptFieldset
          form={form}
          morningSendTime={current.subscriptionReceipt.morningSendTime}
        />
        <WelcomeFieldset form={form} morningSendTime={current.welcome.morningSendTime} />
        <DebtReminderFieldset form={form} />
        <KindFieldset kind="absence" form={form} />
      </KindSection>

      {/* Two boxes and one: side by side on a wide screen, so neither leaves a row half empty. */}
      <div className="grid gap-x-4 gap-y-8 2xl:grid-cols-3">
        <KindSection
          title="پیامک به شما (مدیر)"
          description="به شماره‌ای که در هر بخش می‌نویسید."
          className="2xl:col-span-2"
          columns="md:grid-cols-2"
        >
          <KindFieldset kind="payableDue" form={form}>
            <div className={phoneWidth}>
              <FormField
                label="شماره موبایل مدیر"
                placeholder="۰۹۱۲ ۱۲۳ ۴۵۶۷"
                inputMode="tel"
                dir="ltr"
                className="text-end"
                error={errors.ownerPhone?.message}
                {...form.register("ownerPhone")}
              />
            </div>
          </KindFieldset>
          <DailyReportFieldset form={form} />
        </KindSection>

        <KindSection
          title="ارسال دستی"
          description="به گروهی از اعضا، هر وقت بخواهید."
          columns="md:grid-cols-2 2xl:grid-cols-1"
        >
          <BroadcastFieldset form={form} off={!current.enabled || !current.broadcast.enabled} />
        </KindSection>
      </div>

      <div className="sticky bottom-0 z-10 flex flex-wrap items-center gap-3 border-t bg-background/95 py-3 backdrop-blur">
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

interface KindSectionProps {
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
  /** The grid's columns past a phone's width; three boxes a row on a wide screen by default. */
  columns?: string;
}

/** A group of kinds by who receives them: the members, the Owner, or the broadcast. */
function KindSection({
  title,
  description,
  children,
  className,
  columns = "md:grid-cols-2 2xl:grid-cols-3",
}: KindSectionProps) {
  const titleId = useId();

  return (
    <section aria-labelledby={titleId} className={cn("space-y-3", className)}>
      <div className="space-y-1">
        <h3 id={titleId} className="text-lg font-semibold">
          {title}
        </h3>
        {description !== undefined && (
          <p className="text-sm text-muted-foreground">{description}</p>
        )}
      </div>
      <div className={cn("grid items-stretch gap-4", columns)}>{children}</div>
    </section>
  );
}

interface KindCardProps {
  title: string;
  /** The kind's switch, at the card's top beside its title. */
  toggle: ReactNode;
  description: ReactNode;
  /** The fields, and what the switch has to say (fill the fields first, or the save's refusal). */
  children?: ReactNode;
  /** The message as it reaches the phone; the broadcast draws its own, as the Owner types. */
  preview?: ReactNode;
  id?: string;
}

/**
 * One kind as a box in the section's grid: title and switch, what it does, its few small fields,
 * and its message at the foot, so the boxes of a row line up. A fieldset named by its title, so the
 * fields inside are announced as this kind's.
 */
function KindCard({ title, toggle, description, children, preview, id }: KindCardProps) {
  const titleId = useId();

  return (
    <fieldset
      id={id}
      aria-labelledby={titleId}
      className="flex min-w-0 scroll-mt-4 flex-col gap-4 rounded-xl border bg-card p-4 shadow-sm"
    >
      <div className="flex items-center justify-between gap-3">
        <h4 id={titleId} className="font-semibold">
          {title}
        </h4>
        {toggle}
      </div>
      <p className="text-sm text-muted-foreground">{description}</p>
      {children}
      {preview !== undefined && <div className="mt-auto border-t pt-4">{preview}</div>}
    </fieldset>
  );
}

interface OnSwitchProps extends Omit<ComponentProps<"input">, "type"> {
  /** The checkbox's name; «روشن» for each kind. */
  label?: string;
}

/** A kind's on/off: a checkbox in a pill that fills in when it is on. */
function OnSwitch({ label = "روشن", className, ...input }: OnSwitchProps) {
  return (
    <label
      className={cn(
        "flex w-fit shrink-0 cursor-pointer items-center gap-2 rounded-full border px-3 py-1.5 text-sm",
        "has-checked:border-primary has-checked:bg-primary/10 has-checked:font-medium",
        "has-disabled:cursor-not-allowed has-disabled:opacity-60",
        className,
      )}
    >
      <input type="checkbox" className="size-4 accent-primary" {...input} />
      {label}
    </label>
  );
}

/** What a switch that cannot be turned on yet, or was refused, says under the description. */
function SwitchNotes({ blocked, error }: { blocked: string | null; error?: string }) {
  if (blocked === null && error === undefined) {
    return null;
  }

  return (
    <div className="space-y-1">
      {blocked !== null && <p className="text-sm text-muted-foreground">{blocked}</p>}
      {error !== undefined && <p className="text-sm text-destructive">{error}</p>}
    </div>
  );
}

/** «۱ بخش · نام و عددها نمونه‌اند»: what a sample's caption says. */
function sampleCaption(parts: number | string | undefined): string {
  const example = "نام و عددها نمونه‌اند";

  return parts === undefined ? example : `${toPersianDigits(Number(parts))} بخش · ${example}`;
}

/** The kind's message from `GET /api/sms/samples`, written by the API's own `SmsText`. */
function SamplePreview({ kind }: { kind: keyof SmsSamples }) {
  const samples = useSmsSamples();
  const sample = samples.data?.[kind];

  return (
    <SmsPreview
      text={samples.isError ? "پیش‌نمایش خوانده نشد." : sample?.text}
      caption={sampleCaption(sample?.parts)}
    />
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
    <KindCard
      title={text.title}
      description={text.description}
      preview={<SamplePreview kind={kind} />}
      toggle={
        <Controller
          control={control}
          name={`${kind}.enabled`}
          render={({ field }) => (
            <OnSwitch
              name={field.name}
              checked={field.value}
              disabled={!field.value && !filled}
              aria-invalid={errors?.enabled !== undefined}
              onChange={(event) => field.onChange(event.target.checked)}
              onBlur={field.onBlur}
            />
          )}
        />
      }
    >
      <SwitchNotes
        blocked={
          !values.enabled && !filled
            ? "برای روشن کردن، اول همهٔ خانه‌های این بخش را پر کنید."
            : null
        }
        error={errors?.enabled?.message}
      />

      <div className="flex flex-wrap items-end gap-3">
        <div className={numberWidth}>
          <FormField
            label={text.thresholdLabel}
            placeholder={text.range}
            inputMode="numeric"
            error={errors?.threshold?.message}
            {...form.register(`${kind}.threshold`)}
          />
        </div>

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
    </KindCard>
  );
}

interface LowSessionsFieldsetProps {
  form: UseFormReturn<SmsSettingsValues>;
}

/**
 * «جلسات رو به اتمام» by the plan's size (BUSINESS_RULES.md §10, task 10.17): three short rows, the
 * first from the smallest plan (5) to a size the Owner types, the second up to another, the third
 * every bigger plan, each with its own number of sessions left. The switch follows the other kinds'
 * rule: it cannot be turned on while a box is empty.
 */
function LowSessionsFieldset({ form }: LowSessionsFieldsetProps) {
  const control = form.control;
  const values = useWatch({ control, name: "lowSessions" });
  const filled = isLowSessionsFilled(values);
  const errors = form.formState.errors.lowSessions;
  const small = Number(normalizeDigits(values.smallPlanMax.trim()));
  const medium = Number(normalizeDigits(values.mediumPlanMax.trim()));
  const secondFrom =
    values.smallPlanMax.trim() === "" || Number.isNaN(small) ? "…" : toPersianDigits(small + 1);
  const thirdFrom =
    values.mediumPlanMax.trim() === "" || Number.isNaN(medium) ? "…" : toPersianDigits(medium + 1);
  const left = `${toPersianDigits(lowSessionsLimits.threshold.min)} تا ${toPersianDigits(lowSessionsLimits.threshold.max)}`;

  return (
    <KindCard
      title="جلسات رو به اتمام"
      description="به عضو، وقتی جلسات باقی‌ماندهٔ اشتراک فعالش به عدد ردیف پلنش یا کمتر برسد؛ هر پلن به اندازهٔ خودش. عضوی که اشتراک بعدی را خریده پیامک نمی‌گیرد."
      preview={<SamplePreview kind="lowSessions" />}
      toggle={
        <Controller
          control={control}
          name="lowSessions.enabled"
          render={({ field }) => (
            <OnSwitch
              name={field.name}
              checked={field.value}
              disabled={!field.value && !filled}
              aria-invalid={errors?.enabled !== undefined}
              onChange={(event) => field.onChange(event.target.checked)}
              onBlur={field.onBlur}
            />
          )}
        />
      }
    >
      <SwitchNotes
        blocked={
          !values.enabled && !filled
            ? "برای روشن کردن، اول همهٔ خانه‌های این بخش را پر کنید."
            : null
        }
        error={errors?.enabled?.message}
      />

      <div className="space-y-3">
        <LowSessionsRow title={`پلن ${toPersianDigits(lowSessionsLimits.smallestPlan)} تا`}>
          <div className={rowNumberWidth}>
            <FormField
              label="تا چند جلسه"
              placeholder="۸"
              inputMode="numeric"
              error={errors?.smallPlanMax?.message}
              {...form.register("lowSessions.smallPlanMax")}
            />
          </div>
          <div className={rowNumberWidth}>
            <FormField
              label="چند جلسه مانده"
              placeholder={left}
              inputMode="numeric"
              error={errors?.smallThreshold?.message}
              {...form.register("lowSessions.smallThreshold")}
            />
          </div>
        </LowSessionsRow>

        <LowSessionsRow title={`پلن ${secondFrom} تا`}>
          <div className={rowNumberWidth}>
            <FormField
              label="تا چند جلسه"
              placeholder="۱۵"
              inputMode="numeric"
              error={errors?.mediumPlanMax?.message}
              {...form.register("lowSessions.mediumPlanMax")}
            />
          </div>
          <div className={rowNumberWidth}>
            <FormField
              label="چند جلسه مانده"
              placeholder={left}
              inputMode="numeric"
              error={errors?.mediumThreshold?.message}
              {...form.register("lowSessions.mediumThreshold")}
            />
          </div>
        </LowSessionsRow>

        <LowSessionsRow title={`پلن ${thirdFrom} جلسه و بیشتر`}>
          <div className={rowNumberWidth}>
            <FormField
              label="چند جلسه مانده"
              placeholder={left}
              inputMode="numeric"
              error={errors?.largeThreshold?.message}
              {...form.register("lowSessions.largeThreshold")}
            />
          </div>
        </LowSessionsRow>
      </div>

      <Controller
        control={control}
        name="lowSessions.sendTime"
        render={({ field }) => (
          <SendTimeField
            value={field.value}
            onChange={field.onChange}
            error={errors?.sendTime?.message}
          />
        )}
      />
    </KindCard>
  );
}

/** A plan size's or a number of sessions' box in a row: two digits and its label. */
const rowNumberWidth = "w-28";

/** One row of «جلسات رو به اتمام»: named by its plans, so its boxes are announced as that row's. */
function LowSessionsRow({ title, children }: { title: string; children: ReactNode }) {
  const titleId = useId();

  return (
    <div role="group" aria-labelledby={titleId} className="flex flex-wrap items-end gap-3">
      <p id={titleId} className="w-24 pb-2 text-sm font-medium">
        {title}
      </p>
      {children}
    </div>
  );
}

interface DailyReportFieldsetProps {
  form: UseFormReturn<SmsSettingsValues>;
}

/**
 * The Owner's daily report (BUSINESS_RULES.md §10 *The daily report*, tasks 10.7 and 10.17): a switch,
 * the number it goes to, apart from the cheques' number, and its time, from 08:00 to 23:45. It is
 * about that same day, up to the moment it goes.
 */
function DailyReportFieldset({ form }: DailyReportFieldsetProps) {
  const control = form.control;
  const report = useWatch({ control, name: "dailyReport" });
  const filled = report.phone.trim() !== "";
  const errors = form.formState.errors.dailyReport;

  return (
    <KindCard
      title="گزارش روزانه"
      description="هر روز در ساعتی که انتخاب می‌کنید، برای همان روز تا همان لحظه: تعداد ورود اعضا، پلن و تک‌جلسهٔ فروخته‌شده، و دریافتی (بدون فروشگاه، آنالیز و متفرقه) با نقد، کارت و انتقال، به هزار تومان. آنچه بعد از آن ساعت ثبت شود در هیچ گزارشی نمی‌آید. روزی که هیچ ورود، فروش و پولی نداشته فرستاده نمی‌شود."
      preview={<SamplePreview kind="dailyReport" />}
      toggle={
        <Controller
          control={control}
          name="dailyReport.enabled"
          render={({ field }) => (
            <OnSwitch
              name={field.name}
              checked={field.value}
              disabled={!field.value && !filled}
              aria-invalid={errors?.enabled !== undefined}
              onChange={(event) => field.onChange(event.target.checked)}
              onBlur={field.onBlur}
            />
          )}
        />
      }
    >
      <SwitchNotes
        blocked={
          !report.enabled && !filled ? "برای روشن کردن، اول شمارهٔ موبایل را بنویسید." : null
        }
        error={errors?.enabled?.message}
      />

      <div className="flex flex-wrap items-end gap-3">
        <div className={phoneWidth}>
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

        <Controller
          control={control}
          name="dailyReport.sendTime"
          render={({ field }) => (
            <SendTimeField
              value={field.value}
              onChange={field.onChange}
              error={errors?.sendTime?.message}
              hours={reportHours}
              lastHour={null}
              required
            />
          )}
        />
      </div>
    </KindCard>
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
    <KindCard
      title="رسید خرید اشتراک"
      description={`وقتی پول یک پلن کامل پرداخت شد، به خود عضو: تعداد جلسه، تاریخ شروع و پایان، و مبلغ. همان لحظه فرستاده می‌شود؛ پرداختی که بعد از ساعت ۲۲ ثبت شود، صبح روز بعد ساعت ${morning}. تک‌جلسه و اشتراک رایگان رسید ندارند. پلنی که برای بعد خریده شده، به‌جای تاریخ‌ها تعداد روزش را می‌گوید.`}
      preview={<SamplePreview kind="subscriptionReceipt" />}
      toggle={<OnSwitch {...form.register("subscriptionReceipt.enabled")} />}
    />
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
    <KindCard
      title="خوش‌آمد"
      description={`یک بار برای هر عضو، وقتی اولین پلنش فروخته شد، پرداخت‌شده یا نسیه. همان لحظه فرستاده می‌شود؛ فروشی که بعد از ساعت ۲۲ ثبت شود، صبح روز بعد ساعت ${morning}. عضوی که قبلاً پلن داشته، و تک‌جلسه، خوش‌آمد نمی‌گیرد.`}
      preview={<SamplePreview kind="welcome" />}
      toggle={<OnSwitch {...form.register("welcome.enabled")} />}
    />
  );
}

interface BroadcastFieldsetProps {
  form: UseFormReturn<SmsSettingsValues>;
  /** All SMS or the broadcast is off in the saved settings, not in the unsaved form. */
  off: boolean;
}

/**
 * The Owner's broadcast (BUSINESS_RULES.md §10 *The broadcast*, tasks 10.13 and 10.15), all in one
 * card: its switch, which is saved with the settings, and writing and sending one, which is not:
 * the text, the group and «بررسی و ارسال» act on their own. While the latest broadcast is sending or
 * stopped, it shows under them, with «ادامه ارسال».
 */
function BroadcastFieldset({ form, off }: BroadcastFieldsetProps) {
  const [notice, setNotice] = useState<BroadcastNotice | null>(null);

  return (
    <KindCard
      id={broadcastCardId}
      title="پیامک گروهی"
      description="متنی که خودتان می‌نویسید، با نام کوچک هر عضو در ابتدا، به همهٔ اعضای فعال یا فقط اعضای دارای پلن؛ همان لحظه و فقط بین ساعت ۸ تا ۲۲. پیش از فرستادن، تعداد گیرندگان و هزینه را می‌بینید."
      toggle={<OnSwitch {...form.register("broadcast.enabled")} />}
    >
      {notice !== null && (
        <Alert variant={notice.tone === "success" ? "success" : "destructive"} role="status">
          {notice.text}
        </Alert>
      )}
      <BroadcastForm off={off} onSent={setNotice} />
      <LatestBroadcast />
    </KindCard>
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
    <KindCard
      title="یادآوری بدهی"
      description="به عضوی که قدیمی‌ترین بدهی پرداخت‌نشده‌اش این تعداد روز یا بیشتر مانده و کل بدهی‌اش دست‌کم این مبلغ است، با کل مانده‌اش. برای هر بدهی یک بار؛ پیامک بعدی فقط وقتی می‌رود که همهٔ بدهی آن روز پرداخت شده باشد و بدهی تازه‌ای این‌قدر بماند. عضو غیرفعال هم می‌گیرد."
      preview={<SamplePreview kind="debtReminder" />}
      toggle={
        <Controller
          control={control}
          name="debtReminder.enabled"
          render={({ field }) => (
            <OnSwitch
              name={field.name}
              checked={field.value}
              disabled={!field.value && !filled}
              aria-invalid={errors?.enabled !== undefined}
              onChange={(event) => field.onChange(event.target.checked)}
              onBlur={field.onBlur}
            />
          )}
        />
      }
    >
      <SwitchNotes
        blocked={
          !values.enabled && !filled
            ? "برای روشن کردن، اول همهٔ خانه‌های این بخش را پر کنید."
            : null
        }
        error={errors?.enabled?.message}
      />

      <div className="flex flex-wrap items-end gap-3">
        <div className={numberWidth}>
          <FormField
            label="چند روز پس از بدهی"
            placeholder="۱ تا ۹۰"
            inputMode="numeric"
            error={errors?.daysAfter?.message}
            {...form.register("debtReminder.daysAfter")}
          />
        </div>

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
            <div className="w-56">
              <MoneyField
                label="حداقل کل بدهی (تومان، از ۱٬۰۰۰)"
                placeholder="۲۰۰٬۰۰۰"
                error={errors?.minAmount?.message}
                name={field.name}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
              />
            </div>
          )}
        />
      </div>
    </KindCard>
  );
}

interface SendTimeFieldProps {
  /** `HH:mm`, or empty. */
  value: string;
  onChange: (value: string) => void;
  error?: string;
  /** The hours to choose from: ۸ to ۲۲ for the kinds, ۸ to ۲۳ for the daily report. */
  hours?: readonly string[];
  /** The hour that allows only :00; `null` when every hour has its four quarters. */
  lastHour?: string | null;
  /** Always set (the daily report): no empty choice. */
  required?: boolean;
}

/**
 * An hour and a minute on the quarter (decided with the developer, task 10.2). The minute box
 * follows the hour: choosing an hour fills in :00, and for the kinds ۲۲ allows only :00, since 22:00
 * is their last send time; the daily report goes until 23:45 (task 10.17). So the two boxes can only
 * make a time the server accepts.
 */
function SendTimeField({
  value,
  onChange,
  error,
  hours = sendHours,
  lastHour = lastSendHour,
  required = false,
}: SendTimeFieldProps) {
  const [hour = "", minute = ""] = value === "" ? [] : value.split(":");
  const minutes = hour === lastHour ? ["00"] : sendMinutes;

  function chooseHour(next: string) {
    if (next === "") {
      onChange("");
      return;
    }
    const keptMinute = next === lastHour || minute === "" ? "00" : minute;
    onChange(`${next}:${keptMinute}`);
  }

  return (
    <div className="flex gap-2 [&>*]:w-20">
      <SelectField
        label="ساعت ارسال"
        value={hour}
        error={error}
        onChange={(event) => chooseHour(event.target.value)}
      >
        {!required && <option value="">—</option>}
        {hours.map((option) => (
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
