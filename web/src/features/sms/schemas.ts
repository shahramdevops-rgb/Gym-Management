import { z } from "zod";

import { errorMessages } from "@/lib/errors";
import { formatPhone } from "@/lib/format";
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

/** The four kinds, in the order the page shows them (BUSINESS_RULES.md §10 *The four kinds*). */
export const smsKinds = ["subscriptionExpiring", "lowSessions", "birthday", "payableDue"] as const;

export type SmsKind = (typeof smsKinds)[number];

/** Each kind's number range, the same as the API's `SmsSettings.ThresholdRange` (§10, the settings table). */
export const thresholdRanges: Record<SmsKind, { min: number; max: number; code: string }> = {
  subscriptionExpiring: { min: 1, max: 30, code: "Sms.SubscriptionExpiringDaysOutOfRange" },
  lowSessions: { min: 1, max: 10, code: "Sms.LowSessionsOutOfRange" },
  birthday: { min: 0, max: 7, code: "Sms.BirthdayDaysOutOfRange" },
  payableDue: { min: 0, max: 30, code: "Sms.PayableDueDaysOutOfRange" },
};

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

/** What the form sends: every kind, on or off, and the version it was filled from. */
export function toInput(
  values: SmsSettingsValues,
  version: SmsSettings["version"],
): SmsSettingsInput {
  const ownerPhone = normalizeDigits(values.ownerPhone).trim();

  return {
    enabled: values.enabled,
    subscriptionExpiring: kindInput(values.subscriptionExpiring),
    lowSessions: kindInput(values.lowSessions),
    birthday: kindInput(values.birthday),
    payableDue: kindInput(values.payableDue),
    ownerPhone: ownerPhone === "" ? null : ownerPhone,
    version,
  };
}
