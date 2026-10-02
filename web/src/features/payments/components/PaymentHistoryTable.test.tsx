import { render, screen, within } from "@testing-library/react";

import { formatMoney } from "@/lib/format";

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
      settlement: null,
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

    expect(screen.getByText("۱۲ جلسه - ۳۰ روزه")).toBeInTheDocument();
    expect(screen.getByText("هوازی")).toBeInTheDocument();
    expect(screen.getByText("کافه")).toBeInTheDocument();
  });

  it("puts the rows of one settlement under one heading with the whole amount", () => {
    const settlement = { id: crypto.randomUUID(), total: 450000, itemCount: 2 };
    render(
      <PaymentHistoryTable
        payments={[
          payment({ targetKind: "CafeOrder", amount: 50000, method: "Card", settlement }),
          payment({
            targetKind: "ServiceCharge",
            serviceKind: "Cardio",
            amount: 400000,
            method: "Card",
            settlement,
          }),
          payment({ targetKind: "CafeOrder", amount: 20000, method: "Cash" }),
        ]}
      />,
    );

    const heading = screen.getByText("تسویه یکجا").closest("tr")!;
    expect(within(heading).getByText("(۲ قلم)")).toBeInTheDocument();
    expect(within(heading).getByText(formatMoney(450000))).toBeInTheDocument();
    expect(within(heading).getByText("کارت")).toBeInTheDocument();
    // The method is said once, on the heading; the payment on its own keeps its own row.
    expect(screen.getAllByText("کارت")).toHaveLength(1);
    expect(screen.getByText("هوازی").closest("tr")).toHaveTextContent(formatMoney(400000));
    expect(screen.getByText("نقدی").closest("tr")).toHaveTextContent(formatMoney(20000));
  });
});
