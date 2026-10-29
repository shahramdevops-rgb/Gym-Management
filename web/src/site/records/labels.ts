import type { components } from "@/lib/api/schema";
import { toPersianDigits } from "@/lib/format";

/*
 * The Persian words the site's records are shown with. The panel has the same words in its own
 * features; the site keeps its own copies rather than sharing them, so the two stay apart
 * (ADR 0010, decided by the developer for 15.5b). A word changed in the panel is changed here too.
 */

type Schemas = components["schemas"];
export type PlanStatus = Schemas["SubscriptionStatus"];
export type PaymentStatus = Schemas["PaymentStatus"];
export type PaymentMethod = Schemas["PaymentMethod"];
type ServiceKind = Exclude<Schemas["ServiceChargeKind"], null>;
type BadgeTone = "success" | "secondary" | "warning" | "destructive";

/** A plan's three numbers (BUSINESS_RULES.md §3); mirrors the panel's `planLabel`. */
export interface PlanNumbers {
  durationDays: number | string;
  totalSessions: number | string;
  isSingleSession: boolean;
}

/** «۱۲ جلسه - ۳۰ روزه», or «تک‌جلسه‌ای» for a single visit: a plan has no name of its own. */
export function planLabel(plan: PlanNumbers): string {
  if (plan.isSingleSession) {
    return "تک‌جلسه‌ای";
  }

  return `${toPersianDigits(plan.totalSessions)} جلسه - ${toPersianDigits(plan.durationDays)} روزه`;
}

/** A plan's calculated status (§4), in the panel's words and colours. */
export const planStatus: Record<PlanStatus, { label: string; tone: BadgeTone }> = {
  Active: { label: "فعال", tone: "success" },
  Upcoming: { label: "در انتظار شروع", tone: "secondary" },
  Frozen: { label: "فریز", tone: "secondary" },
  Expired: { label: "منقضی", tone: "secondary" },
  Exhausted: { label: "جلسات تمام‌شده", tone: "warning" },
  Cancelled: { label: "لغوشده", tone: "destructive" },
};

/** What is paid of a plan, a charge or a purchase (§4 "Payment status (calculated)"). */
export const paymentStatus: Record<PaymentStatus, { label: string; tone: BadgeTone }> = {
  Paid: { label: "پرداخت‌شده", tone: "success" },
  Partial: { label: "پرداخت جزئی", tone: "secondary" },
  Unpaid: { label: "پرداخت‌نشده", tone: "destructive" },
};

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  Cash: "نقدی",
  Card: "کارت",
  BankTransfer: "انتقال بانکی",
};

const serviceKindLabels: Record<ServiceKind, string> = {
  Cardio: "هوازی",
  Miscellaneous: "فروشگاه",
  Analysis: "آنالیز",
  Other: "متفرقه",
};

/** A charge's kind, with what a sale sold after it: «هوازی», «فروشگاه: دستکش». */
export function serviceLabel(kind: ServiceKind | null, description: string | null | undefined) {
  if (kind === null) {
    return "خدمات";
  }
  const label = serviceKindLabels[kind];

  return description === null || description === undefined ? label : `${label}: ${description}`;
}

export const cafeLabel = "بوفه";

/** A purchase's lines in one: «قهوه ×۲، کیک». */
export function cafeItemsLine(items: { productName: string; quantity: number | string }[]) {
  return items
    .map((line) =>
      Number(line.quantity) > 1
        ? `${line.productName} ×${toPersianDigits(line.quantity)}`
        : line.productName,
    )
    .join("، ");
}
