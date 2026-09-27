import { debtItem, serviceChargeDebtItem } from "@/test/members";

import { debtBySource } from "./debtBySource";

describe("debtBySource", () => {
  it("DebtBySource_EverySource_AddsEachUpAndListsThePlanFirst", () => {
    const cafe = { ...debtItem(), kind: "CafeOrder" as const, plan: null, outstanding: 40000 };

    const sources = debtBySource([
      cafe,
      serviceChargeDebtItem({ outstanding: 10000 }),
      debtItem({ outstanding: 600000 }),
      { ...cafe, id: "second", outstanding: "25000.50" },
    ]);

    expect(sources).toEqual([
      { label: "بدهی پلن", amount: "600000.00" },
      { label: "بدهی هوازی", amount: "10000.00" },
      { label: "بدهی بوفه", amount: "65000.50" },
    ]);
  });

  it("DebtBySource_SourceThatOwesNothing_IsLeftOut", () => {
    expect(debtBySource([debtItem({ outstanding: 0 })])).toEqual([]);
  });
});
