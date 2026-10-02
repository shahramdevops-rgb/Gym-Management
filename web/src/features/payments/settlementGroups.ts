import type { components } from "@/lib/api/schema";

export type SettlementSummary = components["schemas"]["SettlementSummary"];

/**
 * One line of a payment history: a payment, refund or «تسویه یکجا» row on its own, or the rows of
 * one «تسویه یکجا» together under one heading (BUSINESS_RULES.md §5 Settling several items at once).
 */
export type PaymentRowGroup<T> =
  { kind: "single"; item: T } | { kind: "settlement"; settlement: SettlementSummary; items: T[] };

/**
 * Gathers neighbouring rows of the same settlement. They are always neighbours: the API lists
 * newest first and every row of one settlement has the same moment. A settlement with only one row
 * left on this page, or let through by the filters, stays a plain row: a heading over one item says
 * nothing the row does not.
 */
export function groupBySettlement<T extends { settlement: SettlementSummary | null }>(
  items: T[],
): PaymentRowGroup<T>[] {
  const groups: PaymentRowGroup<T>[] = [];

  for (const item of items) {
    const last = groups.at(-1);

    if (last?.kind === "settlement" && last.settlement.id === item.settlement?.id) {
      last.items.push(item);
      continue;
    }

    if (
      last?.kind === "single" &&
      item.settlement !== null &&
      last.item.settlement?.id === item.settlement.id
    ) {
      groups[groups.length - 1] = {
        kind: "settlement",
        settlement: item.settlement,
        items: [last.item, item],
      };
      continue;
    }

    groups.push({ kind: "single", item });
  }

  return groups;
}
