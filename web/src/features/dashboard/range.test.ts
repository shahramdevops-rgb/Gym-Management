import { presetOf, presetRange, rangeDays, rangeError } from "./range";

// 2026-10-04 is Sunday ۱۴۰۵/۰۷/۱۲.
const today = "2026-10-04";

describe("presetRange", () => {
  it("presetRange_Today_IsOneDay", () => {
    expect(presetRange("today", today)).toEqual({ from: today, to: today });
  });

  it("presetRange_WeekOnSunday_StartsOnTheSaturdayBefore", () => {
    expect(presetRange("week", today)).toEqual({ from: "2026-10-03", to: today });
  });

  it("presetRange_WeekOnSaturday_IsThatDayAlone", () => {
    expect(presetRange("week", "2026-10-03")).toEqual({ from: "2026-10-03", to: "2026-10-03" });
  });

  it("presetRange_WeekOnFriday_StartsSixDaysBefore", () => {
    expect(presetRange("week", "2026-10-09")).toEqual({ from: "2026-10-03", to: "2026-10-09" });
  });

  it("presetRange_Month_RunsFromTheFirstOfTheJalaliMonthToToday", () => {
    // ۱۴۰۵/۰۷/۰۱ to ۱۴۰۵/۰۷/۱۲, the example of §12 Financial report.
    expect(presetRange("month", today)).toEqual({ from: "2026-09-23", to: today });
  });

  it("presetRange_LastMonth_IsTheWholeMonthBefore", () => {
    // شهریور ۱۴۰۵ has 31 days: ۰۶/۰۱ to ۰۶/۳۱.
    expect(presetRange("lastMonth", today)).toEqual({ from: "2026-08-23", to: "2026-09-22" });
  });

  it("presetRange_LastMonthInFarvardin_IsEsfandOfTheYearBefore", () => {
    // ۱۴۰۵/۰۱/۱۲; اسفند ۱۴۰۴ is a common year's, 29 days.
    expect(presetRange("lastMonth", "2026-03-31")).toEqual({
      from: "2026-02-20",
      to: "2026-03-20",
    });
  });

  it("presetRange_LastMonthAfterALeapEsfand_EndsOnItsThirtieth", () => {
    // ۱۴۰۴/۰۱/۱۰; اسفند ۱۴۰۳ had 30 days (۱۴۰۳ is a leap year).
    expect(presetRange("lastMonth", "2025-03-30")).toEqual({
      from: "2025-02-19",
      to: "2025-03-20",
    });
  });

  it("presetRange_Year_RunsFromNowruzToToday", () => {
    expect(presetRange("year", today)).toEqual({ from: "2026-03-21", to: today });
  });
});

describe("presetOf", () => {
  it("presetOf_RangeOfAPreset_NamesIt", () => {
    expect(presetOf({ from: "2026-08-23", to: "2026-09-22" }, today)).toBe("lastMonth");
  });

  it("presetOf_RangeOfOnesOwn_IsUndefined", () => {
    expect(presetOf({ from: "2026-09-01", to: "2026-09-30" }, today)).toBeUndefined();
  });

  it("presetOf_TodayOnASaturday_IsToday", () => {
    // On a Saturday "today" and "this week" are the same day; the first one listed wins.
    expect(presetOf({ from: "2026-10-03", to: "2026-10-03" }, "2026-10-03")).toBe("today");
  });
});

describe("rangeError", () => {
  it("rangeError_ADateMissing_IsRequired", () => {
    expect(rangeError({ from: "2026-09-01" })).toBe("Reports.DateRangeRequired");
    expect(rangeError({ to: "2026-09-01" })).toBe("Reports.DateRangeRequired");
  });

  it("rangeError_Backwards_IsInvalid", () => {
    expect(rangeError({ from: "2026-09-02", to: "2026-09-01" })).toBe("Reports.InvalidDateRange");
  });

  it("rangeError_366Days_IsAccepted", () => {
    expect(rangeDays({ from: "2025-10-04", to: "2026-10-04" })).toBe(366);
    expect(rangeError({ from: "2025-10-04", to: "2026-10-04" })).toBeUndefined();
  });

  it("rangeError_367Days_IsTooLong", () => {
    expect(rangeError({ from: "2025-10-03", to: "2026-10-04" })).toBe("Reports.RangeTooLong");
  });

  it("rangeError_OneDay_IsAccepted", () => {
    expect(rangeDays({ from: today, to: today })).toBe(1);
    expect(rangeError({ from: today, to: today })).toBeUndefined();
  });
});
