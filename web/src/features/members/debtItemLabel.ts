import { serviceChargeKindLabels } from "@/features/serviceCharges/api";

import type { MemberDebtItem } from "./api";

/**
 * What a debt row is for, in Persian: a subscription reads as its plan's name, a service charge as
 * its kind (هوازی), a cafe order as بوفه. The API sends kinds, never Persian text.
 */
export function debtItemLabel(item: MemberDebtItem): string {
  switch (item.kind) {
    case "ServiceCharge":
      return item.serviceKind === null ? "خدمات" : serviceChargeKindLabels[item.serviceKind];
    case "CafeOrder":
      return "بوفه";
    case "Subscription":
      return `اشتراک ${item.planName ?? ""}`.trim();
  }
}
