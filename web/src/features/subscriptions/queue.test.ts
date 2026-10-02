import { activeSubscription, cancelledRenewal, queuedRenewal } from "@/test/subscriptions";

import { isQueuedBehindAnother } from "./queue";

describe("isQueuedBehindAnother", () => {
  it("isQueuedBehindAnother_UpcomingRightAfterALivePlan_IsTrue", () => {
    expect(isQueuedBehindAnother(queuedRenewal, [queuedRenewal, activeSubscription])).toBe(true);
  });

  it("isQueuedBehindAnother_PlanBeforeItWasCancelled_IsFalse", () => {
    // Cancelling does not move a queued plan earlier (BUSINESS_RULES.md §4 Cancel): it keeps its
    // dates, so they are real and not provisional.
    const cancelledCurrent = { ...activeSubscription, status: "Cancelled" as const };

    expect(isQueuedBehindAnother(queuedRenewal, [queuedRenewal, cancelledCurrent])).toBe(false);
  });

  it("isQueuedBehindAnother_PlanBeforeItNotOnThisPage_IsFalse", () => {
    expect(isQueuedBehindAnother(queuedRenewal, [queuedRenewal])).toBe(false);
  });

  it("isQueuedBehindAnother_GapBeforeIt_IsFalse", () => {
    const endedEarlier = { ...activeSubscription, endDate: "2026-09-29" };

    expect(isQueuedBehindAnother(queuedRenewal, [queuedRenewal, endedEarlier])).toBe(false);
  });

  it("isQueuedBehindAnother_ActivePlan_IsFalse", () => {
    expect(isQueuedBehindAnother(activeSubscription, [queuedRenewal, activeSubscription])).toBe(
      false,
    );
  });

  it("isQueuedBehindAnother_OnlyASingleVisitBeforeIt_IsFalse", () => {
    // A single visit never takes part in the queue (BUSINESS_RULES.md §4 Single-session).
    const singleVisit = {
      ...activeSubscription,
      startDate: "2026-09-30",
      isSingleSession: true,
    };

    expect(isQueuedBehindAnother(queuedRenewal, [queuedRenewal, singleVisit])).toBe(false);
  });

  it("isQueuedBehindAnother_CancelledUpcoming_IsFalse", () => {
    expect(isQueuedBehindAnother(cancelledRenewal, [cancelledRenewal, activeSubscription])).toBe(
      false,
    );
  });
});
