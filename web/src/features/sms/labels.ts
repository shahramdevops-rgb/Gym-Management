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
  Cancelled: "کاوه‌نگار لغو کرد",
};

/** Kavenegar gives the cost of these back, so the message costs nothing (BUSINESS_RULES.md §10 *Sending*). */
export function isRefunded(delivery: SmsMessage["delivery"]): boolean {
  return delivery === "BlockedByReceiver" || delivery === "Cancelled";
}

/**
 * What Kavenegar's codes mean, for the ones BUSINESS_RULES.md §10 *Sending* names (from Kavenegar's
 * documentation). Any other code is shown as a number: it is kept so it can be looked up there.
 */
const kavenegarCodeMeanings: Record<number, string> = {
  401: "حساب کاوه‌نگار غیرفعال است",
  403: "کلید API کاوه‌نگار نامعتبر است",
  407: "IP سرور در تنظیمات امنیتی کاوه‌نگار ثبت نشده",
  409: "کاوه‌نگار مشغول بود",
  411: "شماره نامعتبر است",
  412: "خط ارسال برای این حساب معتبر نیست",
  413: "متن پیامک خالی یا بیش از حد بلند است",
  416: "IP سرور با تنظیمات کاوه‌نگار نمی‌خواند",
  418: "اعتبار پنل تمام شده",
  420: "لینک در متن پیامک برای این حساب مجاز نیست",
  422: "نویسهٔ نامعتبر در متن",
  427: "این خط برای حساب دسترسی ندارد",
  429: "IP سرور در کاوه‌نگار محدود شده",
  451: "درخواست بیش از حد به کاوه‌نگار",
  501: "حساب فقط به شمارهٔ صاحب حساب پیامک آزمایشی می‌فرستد",
};

/** `۴۱۱ — شماره نامعتبر است`, or just the code when its meaning is not known here. */
export function describeSmsError(code: SmsMessage["errorCode"]): string | null {
  if (code === null) {
    return null;
  }

  const meaning = kavenegarCodeMeanings[Number(code)];

  return meaning === undefined
    ? `کد ${toPersianDigits(code)}`
    : `کد ${toPersianDigits(code)} — ${meaning}`;
}
