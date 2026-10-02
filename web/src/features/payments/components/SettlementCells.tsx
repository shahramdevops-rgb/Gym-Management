import type { ReactNode } from "react";

import { toPersianDigits } from "@/lib/format";

import type { SettlementSummary } from "../settlementGroups";

/** The «بابت» of a settlement's heading row: what it was, and how many items it paid. */
export function SettlementHeading({ settlement }: { settlement: SettlementSummary }) {
  return (
    <span className="flex flex-wrap items-center gap-2 font-medium">
      تسویه یکجا
      <span className="text-xs font-normal text-muted-foreground">
        ({toPersianDigits(settlement.itemCount)} قلم)
      </span>
    </span>
  );
}

/** The «بابت» of one item under a settlement's heading, set one step in. */
export function SettlementItemLabel({ children }: { children: ReactNode }) {
  return <span className="ms-4 flex flex-wrap items-center gap-2 border-s-2 ps-3">{children}</span>;
}
