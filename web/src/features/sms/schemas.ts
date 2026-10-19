import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { formatPhone } from "@/lib/format";
import { normalizeMoney } from "@/lib/money";
import { normalizeDigits } from "@/lib/normalize";

import type { SmsKindSettings, SmsSettings, SmsSettingsInput } from "./api";

/** The Persian text for a code. Throws at import if the catalogue lacks it, so a typo fails every test. */
function message(code: string): string {
  const text = errorMessages[code];
  if (text === undefined) {
    throw new Error(`No Persian message for ${code} in lib/errors.ts.`);
  }
  return text;
}

/**
 * The kinds with a number and a send time (BUSINESS_RULES.md §10 *The four kinds*, and *The absence
 * SMS*, task 10.12). The page shows the absence SMS last, after the debt reminder.
 */
export const smsKinds = [
  "subscriptionExpiring",
  "lowSessions",
  "birthday",
  "payableDue",
  "absence",
] as const;

export type SmsKind = (typeof smsKinds)[number];

/** Each kind's number range, the same as the API's `SmsSettings.ThresholdRange` (§10, the settings table). */
export const thresholdRanges: Record<SmsKind, { min: number; max: number; code: string }> = {
  subscriptionExpiring: { min: 1, max: 30, code: "Sms.SubscriptionExpiringDaysOutOfRange" },
  lowSessions: { min: 1, max: 10, code: "Sms.LowSessionsOutOfRange" },
  birthday: { min: 1, max: 7, code: "Sms.BirthdayDaysOutOfRange" },
  payableDue: { min: 0, max: 30, code: "Sms.PayableDueDaysOutOfRange" },
  absence: { min: 7, max: 60, code: "Sms.AbsenceDaysOutOfRange" },
};

/** The debt reminder's days since the oldest unpaid sale, the same as the API's `SmsSettings.ThresholdRange` (task 10.11). */
export const debtReminderDaysRange = { min: 1, max: 90 } as const;

/** The smallest minimum amount, in Toman (`SmsSettings.DebtReminderMinAmountFloor`). */
export const debtReminderMinAmountFloor = 1000;

/** A `numeric(18,2)` amount: sixteen digits before the point, two after (§3). */
const amountLimits = { decimals: 2, integerDigits: 16 } as const;

/**
 * Whether a typed minimum amount is one the API takes: 1,000 Toman or more, at most two decimal
 * places, refused rather than rounded (BUSINESS_RULES.md §10 *The debt reminder*).
 */
export function isValidDebtReminderMinAmount(text: string): boolean {
  const parts = /^(\d+)(?:\.(\d+))?$/.exec(normalizeMoney(text));
  if (parts === null) {
    return false;
  }

  const [, whole = "", fraction = ""] = parts;
  const digits = whole.replace(/^0+(?=\d)/, "");

  return (
    fraction.length <= amountLimits.decimals &&
    digits.length <= amountLimits.integerDigits &&
    // Below 1,000 has at most four digits, so this compares as a number without losing anything.
    (digits.length > 4 || Number(digits) >= debtReminderMinAmountFloor)
  );
}

/** The hours a send time can start in: 08 to 22 (Asia/Tehran). */
export const sendHours = Array.from({ length: 15 }, (_, index) =>
  String(index + 8).padStart(2, "0"),
);

/** Send times fall on a quarter hour (decided with the developer, task 10.2). */
export const sendMinutes = ["00", "15", "30", "45"] as const;

/** 22:00 is the last send time, so 22 has no quarter after it. */
export const lastSendHour = "22";

const kindSchema = z.object({
  enabled: z.boolean(),
  /** As typed, Persian or English digits; empty while the Owner has not chosen it. */
  threshold: z.string(),
  /** `HH:mm`, or empty. The hour and minute boxes can only make a valid time. */
  sendTime: z.string(),
});

export type SmsKindValues = z.infer<typeof kindSchema>;

function wholeNumber(text: string): number | null {
  const normalized = normalizeDigits(text).trim();

  return /^\d{1,3}$/.test(normalized) ? Number(normalized) : null;
}

const debtReminderSchema = z.object({
  enabled: z.boolean(),
  /** As typed, Persian or English digits; empty while the Owner has not chosen it. */
  daysAfter: z.string(),
  /** `HH:mm`, or empty. */
  sendTime: z.string(),
  /** As typed in `MoneyField`; empty while the Owner has not chosen it. */
  minAmount: z.string(),
});

export type DebtReminderValues = z.infer<typeof debtReminderSchema>;

/** Whether the debt reminder has its days, time and minimum amount, so its switch may be turned on. */
export function isDebtReminderFilled(values: DebtReminderValues): boolean {
  return values.daysAfter.trim() !== "" && values.sendTime !== "" && values.minAmount.trim() !== "";
}

/** Whether a kind has its number and time, so its switch may be turned on. */
export function isKindFilled(kind: SmsKind, values: SmsKindValues, ownerPhone: string): boolean {
  const filled = values.threshold.trim() !== "" && values.sendTime !== "";

  // The cheque and instalment reminders go to the Owner, so they also need the Owner's number.
  return filled && (kind !== "payableDue" || ownerPhone.trim() !== "");
}

/**
 * The whole page, saved at once. The checks are the API's, each under its own field: a filled
 * number in its kind's range, and a kind turned on only when it is filled (§10). Whether the Owner's number is a real Iranian mobile only the
 * server can say (libphonenumber); its answer is shown under the number.
 */
export const smsSettingsSchema = z
  .object({
    enabled: z.boolean(),
    subscriptionExpiring: kindSchema,
    lowSessions: kindSchema,
    birthday: kindSchema,
    payableDue: kindSchema,
    ownerPhone: z.string().max(30, message("Members.PhoneInvalid")),
    /** The daily report (task 10.7): only a switch and the number it goes to; its time is fixed. */
    dailyReport: z.object({
      enabled: z.boolean(),
      phone: z.string().max(30, message("Sms.DailyReportPhoneInvalid")),
    }),
    /** The member's receipt (task 10.9): only a switch, nothing to fill in. */
    subscriptionReceipt: z.object({
      enabled: z.boolean(),
    }),
    /** The member's welcome (task 10.10): only a switch, like the receipt. */
    welcome: z.object({
      enabled: z.boolean(),
    }),
    /** The member's debt reminder (task 10.11): days, time and a minimum amount. */
    debtReminder: debtReminderSchema,
    /** The member's absence SMS (task 10.12): days away and a time, like the four kinds. */
    absence: kindSchema,
    /** The Owner's broadcast (task 10.13): only a switch; the text is written on «پیامک گروهی». */
    broadcast: z.object({
      enabled: z.boolean(),
    }),
  })
  .superRefine((values, context) => {
    for (const kind of smsKinds) {
      const settings = values[kind];

      if (settings.threshold.trim() !== "") {
        const range = thresholdRanges[kind];
        const threshold = wholeNumber(settings.threshold);
        if (threshold === null || threshold < range.min || threshold > range.max) {
          context.addIssue({
            code: "custom",
            path: [kind, "threshold"],
            message: message(range.code),
          });
        }
      }

      if (settings.enabled && !isKindFilled(kind, settings, values.ownerPhone)) {
        context.addIssue({
          code: "custom",
          path: [kind, "enabled"],
          message: message("Sms.SettingsIncomplete"),
        });
      }
    }

    if (values.dailyReport.enabled && values.dailyReport.phone.trim() === "") {
      context.addIssue({
        code: "custom",
        path: ["dailyReport", "enabled"],
        message: message("Sms.SettingsIncomplete"),
      });
    }

    const debt = values.debtReminder;
    if (debt.daysAfter.trim() !== "") {
      const days = wholeNumber(debt.daysAfter);
      if (days === null || days < debtReminderDaysRange.min || days > debtReminderDaysRange.max) {
        context.addIssue({
          code: "custom",
          path: ["debtReminder", "daysAfter"],
          message: message("Sms.DebtReminderDaysOutOfRange"),
        });
      }
    }

    if (debt.minAmount.trim() !== "" && !isValidDebtReminderMinAmount(debt.minAmount)) {
      context.addIssue({
        code: "custom",
        path: ["debtReminder", "minAmount"],
        message: message("Sms.DebtReminderMinAmountOutOfRange"),
      });
    }

    if (debt.enabled && !isDebtReminderFilled(debt)) {
      context.addIssue({
        code: "custom",
        path: ["debtReminder", "enabled"],
        message: message("Sms.SettingsIncomplete"),
      });
    }
  });

export type SmsSettingsValues = z.infer<typeof smsSettingsSchema>;

function kindValues(settings: SmsKindSettings): SmsKindValues {
  return {
    enabled: settings.enabled,
    threshold:
      settings.threshold === null || settings.threshold === undefined
        ? ""
        : String(settings.threshold),
    // The API sends `HH:mm:ss`; the boxes choose hours and minutes.
    sendTime: settings.sendTime?.slice(0, 5) ?? "",
  };
}

/** The form's values for the settings as the API sent them. */
export function toFormValues(settings: SmsSettings): SmsSettingsValues {
  return {
    enabled: settings.enabled,
    subscriptionExpiring: kindValues(settings.subscriptionExpiring),
    lowSessions: kindValues(settings.lowSessions),
    birthday: kindValues(settings.birthday),
    payableDue: kindValues(settings.payableDue),
    // Shown the way people write it (۰۹۱۲ ۱۲۳ ۴۵۶۷); the server reads any digits and spaces.
    ownerPhone: settings.ownerPhone === null ? "" : formatPhone(settings.ownerPhone),
    dailyReport: {
      enabled: settings.dailyReport.enabled,
      phone: settings.dailyReport.phone === null ? "" : formatPhone(settings.dailyReport.phone),
    },
    subscriptionReceipt: { enabled: settings.subscriptionReceipt.enabled },
    welcome: { enabled: settings.welcome.enabled },
    debtReminder: {
      enabled: settings.debtReminder.enabled,
      daysAfter:
        settings.debtReminder.daysAfter === null ? "" : String(settings.debtReminder.daysAfter),
      sendTime: settings.debtReminder.sendTime?.slice(0, 5) ?? "",
      minAmount:
        settings.debtReminder.minAmount === null ? "" : String(settings.debtReminder.minAmount),
    },
    absence: kindValues(settings.absence),
    broadcast: { enabled: settings.broadcast.enabled },
  };
}

function kindInput(values: SmsKindValues): SmsKindSettings {
  const threshold = values.threshold.trim();

  return {
    enabled: values.enabled,
    threshold: threshold === "" ? null : Number(normalizeDigits(threshold)),
    sendTime: values.sendTime === "" ? null : `${values.sendTime}:00`,
  };
}

function debtReminderInput(values: DebtReminderValues): SmsSettingsInput["debtReminder"] {
  const days = values.daysAfter.trim();
  const minAmount = normalizeMoney(values.minAmount);

  return {
    enabled: values.enabled,
    daysAfter: days === "" ? null : Number(normalizeDigits(days)),
    sendTime: values.sendTime === "" ? null : `${values.sendTime}:00`,
    // A string, never a float: an amount is never rounded on its way (§13).
    minAmount: minAmount === "" ? null : minAmount,
  };
}

/** What the form sends: every kind, on or off, and the version it was filled from. */
export function toInput(
  values: SmsSettingsValues,
  version: SmsSettings["version"],
): SmsSettingsInput {
  const ownerPhone = normalizeDigits(values.ownerPhone).trim();
  const reportPhone = normalizeDigits(values.dailyReport.phone).trim();

  return {
    enabled: values.enabled,
    subscriptionExpiring: kindInput(values.subscriptionExpiring),
    lowSessions: kindInput(values.lowSessions),
    birthday: kindInput(values.birthday),
    payableDue: kindInput(values.payableDue),
    ownerPhone: ownerPhone === "" ? null : ownerPhone,
    dailyReport: {
      enabled: values.dailyReport.enabled,
      phone: reportPhone === "" ? null : reportPhone,
    },
    subscriptionReceipt: { enabled: values.subscriptionReceipt.enabled },
    welcome: { enabled: values.welcome.enabled },
    debtReminder: debtReminderInput(values.debtReminder),
    absence: kindInput(values.absence),
    broadcast: { enabled: values.broadcast.enabled },
    version,
  };
}
