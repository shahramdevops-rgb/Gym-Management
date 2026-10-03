import { serviceChargeLabel } from "@/features/serviceCharges/api";
import { planLabel } from "@/features/subscriptions/planLabel";

import type { MemberDebtItem } from "./api";

/**
 * What a debt row is for, in Persian: a subscription reads as its plan («اشتراک ۱۲ جلسه - ۳۰ روزه»),
 * a service charge as its kind (هوازی), a miscellaneous sale with its name («متفرقه: دستکش»), a cafe order as بوفه. The API sends kinds and numbers, never
 * Persian text.
 */
export function debtItemLabel(item: MemberDebtItem): string {
  switch (item.kind) {
    case "ServiceCharge":
      return serviceChargeLabel(item.serviceKind, item.sale?.description);
    case "CafeOrder":
      return "بوفه";
    case "Subscription":
      return item.plan === null ? "اشتراک" : `اشتراک ${planLabel(item.plan)}`;
  }
}
