import { isoDaysBefore, staffEarliestPaymentDay } from "./range";

describe("range", () => {
  it("StaffEarliestPaymentDay_Today_IsThreeDaysBefore", () => {
    // 1405/07/10 → 07/07.
    expect(staffEarliestPaymentDay("2026-10-02")).toBe("2026-09-29");
  });

  it("IsoDaysBefore_AcrossAMonthAndAYear_CountsCalendarDays", () => {
    expect(isoDaysBefore("2026-03-01", 1)).toBe("2026-02-28");
    expect(isoDaysBefore("2026-01-02", 3)).toBe("2025-12-30");
  });

  it("IsoDaysBefore_AcrossIransOldClockChange_StaysOnTheCalendarDay", () => {
    // Iran moved its clocks on 22 March until 2022; a local-time calculation could slip a day.
    expect(isoDaysBefore("2021-03-23", 1)).toBe("2021-03-22");
  });
});
