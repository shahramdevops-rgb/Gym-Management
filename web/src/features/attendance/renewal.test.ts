import { insideRow, openVisit } from "@/test/attendance";

import { renewalDue } from "./renewal";

const today = "2026-08-03";
const visit = openVisit("0199a000-0000-7000-8000-0000000000a1");

describe("renewalDue", () => {
  it("renewalDue_PlentyLeft_ReturnsNull", () => {
    expect(renewalDue(insideRow("رضا", visit), today)).toBeNull();
  });

  it("renewalDue_ThreeSessionsLeft_ReturnsSessions", () => {
    const row = insideRow("رضا", visit, { usedSessions: 9, remainingSessions: 3 });

    expect(renewalDue(row, today)).toEqual({ kind: "sessions", left: 3 });
  });

  it("renewalDue_FourSessionsLeft_ReturnsNull", () => {
    const row = insideRow("رضا", visit, { usedSessions: 8, remainingSessions: 4 });

    expect(renewalDue(row, today)).toBeNull();
  });

  it("renewalDue_EndsInFiveDays_ReturnsDays", () => {
    const row = insideRow("رضا", visit, { subscriptionEndDate: "2026-08-08" });

    expect(renewalDue(row, today)).toEqual({ kind: "days", left: 5 });
  });

  it("renewalDue_EndsInSixDays_ReturnsNull", () => {
    const row = insideRow("رضا", visit, { subscriptionEndDate: "2026-08-09" });

    expect(renewalDue(row, today)).toBeNull();
  });

  it("renewalDue_BothRunningOut_NamesTheSessions", () => {
    const row = insideRow("رضا", visit, {
      usedSessions: 11,
      remainingSessions: 1,
      subscriptionEndDate: today,
    });

    expect(renewalDue(row, today)).toEqual({ kind: "sessions", left: 1 });
  });

  it("renewalDue_SingleVisit_ReturnsNull", () => {
    const row = insideRow("رضا", visit, {
      isSingleSession: true,
      totalSessions: 1,
      usedSessions: 1,
      remainingSessions: 0,
      subscriptionEndDate: today,
    });

    expect(renewalDue(row, today)).toBeNull();
  });

  it("renewalDue_NextSubscriptionAlreadyBought_ReturnsNull", () => {
    const row = insideRow("رضا", visit, {
      usedSessions: 11,
      remainingSessions: 1,
      hasQueuedRenewal: true,
    });

    expect(renewalDue(row, today)).toBeNull();
  });
});
