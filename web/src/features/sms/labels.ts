import { toPersianDigits } from "@/lib/format";

import type { SmsMessage, SmsMessageKind, SmsMessageStatus } from "./api";

/** The four kinds by the names the settings page gives them (BUSINESS_RULES.md §10 *The four kinds*). */
export const smsKindLabels: Record<SmsMessageKind, string> = {
  SubscriptionExpiring: "پایان اشتراک",
  LowSessions: "جلسات رو به اتمام",
  Birthday: "تولد",
  PayableDue: "چک و قسط",
};

export const smsStatusLabels: Record<SmsMessageStatus, string> = {
  Pending: "در حال ارسال",
  Sent: "ارسال شد",
  Failed: "ناموفق",
  Unknown: "نامعلوم",
};

export const smsDeliveryLabels: Record<NonNullable<SmsMessage["delivery"]>, string> = {
  Delivered: "به گوشی رسید",
  NotDelivered: "به گوشی نرسید",
  BlockedByReceiver: "گیرنده مسدود کرده",
};

/**
 * What Kavenegar's codes mean, for the ones BUSINESS_RULES.md §10 *Sending* names. Any other code
 * is shown as a number: it is kept so it can be looked up in Kavenegar's documentation.
 */
const kavenegarCodeMeanings: Record<number, string> = {
  409: "کاوه‌نگار مشغول بود",
  411: "شماره نامعتبر است",
  418: "اعتبار پنل تمام شده",
  422: "نویسهٔ نامعتبر در متن",
  424: "قالب پیدا نشد یا تأیید نشده",
  426: "سرویس پیشرفته فعال نیست",
  431: "نویسهٔ نامعتبر در متن",
};

/** `۴۲۴ — قالب پیدا نشد یا تأیید نشده`, or just the code when its meaning is not known here. */
export function describeSmsError(code: SmsMessage["errorCode"]): string | null {
  if (code === null) {
    return null;
  }

  const meaning = kavenegarCodeMeanings[Number(code)];

  return meaning === undefined
    ? `کد ${toPersianDigits(code)}`
    : `کد ${toPersianDigits(code)} — ${meaning}`;
}
