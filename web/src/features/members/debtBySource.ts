import { serviceChargeKindLabels } from "@/features/serviceCharges/api";
import { addMoney, isPositiveMoney } from "@/lib/money";

import type { MemberDebtItem } from "./api";

export interface DebtSource {
  label: string;
  /** What is still owed for this source, as the plain decimal string `formatMoney` reads. */
  amount: string;
}

/** The heading a debt item is added up under: its plan, its service, or the cafe. */
function sourceOf(item: MemberDebtItem): { order: number; label: string } {
  switch (item.kind) {
    case "Subscription":
      return { order: 0, label: "بدهی پلن" };
    case "ServiceCharge":
      return {
        order: 1,
        label: `بدهی ${item.serviceKind === null ? "خدمات" : serviceChargeKindLabels[item.serviceKind]}`,
      };
    case "CafeOrder":
      return { order: 2, label: "بدهی بوفه" };
  }
}

/**
 * A member's debt added up by where it came from — plan, هوازی, cafe — which is how the desk says
 * it at check-out: "this much for the plan, this much for هوازی, this much for the cafe". Only the
 * sources that owe something are listed, plan first. Exact, like every other sum of money here.
 */
export function debtBySource(items: MemberDebtItem[]): DebtSource[] {
  const sums = new Map<string, { order: number; amount: string }>();
  for (const item of items) {
    const { order, label } = sourceOf(item);
    const current = sums.get(label)?.amount ?? "0";
    sums.set(label, { order, amount: addMoney(current, item.outstanding) });
  }

  return [...sums.entries()]
    .filter(([, sum]) => isPositiveMoney(sum.amount))
    .sort(([, left], [, right]) => left.order - right.order)
    .map(([label, sum]) => ({ label, amount: sum.amount }));
}
