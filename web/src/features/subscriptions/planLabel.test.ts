import { planLabel } from "./planLabel";

describe("planLabel", () => {
  it("planLabel_Membership_ReadsAsItsDaysAndSessionsInPersianDigits", () => {
    expect(planLabel({ durationDays: 30, totalSessions: 12, isSingleSession: false })).toBe(
      "۳۰ روز · ۱۲ جلسه",
    );
  });

  it("planLabel_NumbersSentAsStrings_ReadTheSame", () => {
    // The generated API types widen integers to number | string.
    expect(planLabel({ durationDays: "45", totalSessions: "20", isSingleSession: false })).toBe(
      "۴۵ روز · ۲۰ جلسه",
    );
  });

  it("planLabel_SingleVisit_ReadsAsTakJalase", () => {
    expect(planLabel({ durationDays: 1, totalSessions: 1, isSingleSession: true })).toBe(
      "تک‌جلسه‌ای",
    );
  });
});
