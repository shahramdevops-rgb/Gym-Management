import { render, screen } from "@testing-library/react";

import type { PaymentHistoryItem } from "../api";
import { PaymentHistoryTable } from "./PaymentHistoryTable";

/**
 * The "بابت" column names what each payment went against. Cafe payments joined the member's
 * history in task 7.3; before that the table read anything that was not a service charge as a
 * subscription, so a cafe row would have shown a blank plan name.
 */
describe("PaymentHistoryTable", () => {
  function payment(overrides: Partial<PaymentHistoryItem>): PaymentHistoryItem {
    return {
      id: crypto.randomUUID(),
      targetKind: "Subscription",
      targetId: crypto.randomUUID(),
      subscriptionPlan: null,
      serviceKind: null,
      kind: "Payment",
      amount: "15000",
      method: "Cash",
      referenceNumber: null,
      paidAt: "2026-09-26T08:00:00Z",
      receivedByUserId: crypto.randomUUID(),
      reason: null,
      createdAt: "2026-09-26T08:00:00Z",
      ...overrides,
    };
  }

  it("labels each kind of target", () => {
    render(
      <PaymentHistoryTable
        payments={[
          payment({
            targetKind: "Subscription",
            subscriptionPlan: { durationDays: 30, totalSessions: 12, isSingleSession: false },
          }),
          payment({ targetKind: "ServiceCharge", serviceKind: "Cardio" }),
          payment({ targetKind: "CafeOrder" }),
        ]}
      />,
    );

    expect(screen.getByText("۳۰ روز · ۱۲ جلسه")).toBeInTheDocument();
    expect(screen.getByText("هوازی")).toBeInTheDocument();
    expect(screen.getByText("کافه")).toBeInTheDocument();
  });
});
