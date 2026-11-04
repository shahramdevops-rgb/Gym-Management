import { formatClock, formatMonth, monthOf, monthRange, sameMonth, shiftMonth } from "./labels";

describe("attendance labels", () => {
  it("MonthOf_AnIsoDate_GivesItsJalaliMonth", () => {
    expect(monthOf("2026-11-04")).toEqual({ year: 1405, month: 8 });
  });

  it("MonthRange_Aban_RunsThirtyDays", () => {
    expect(monthRange({ year: 1405, month: 8 })).toEqual({ from: "2026-10-23", to: "2026-11-21" });
  });

  it("MonthRange_Farvardin_RunsThirtyOneDays", () => {
    expect(monthRange({ year: 1405, month: 1 })).toEqual({ from: "2026-03-21", to: "2026-04-20" });
  });

  it("ShiftMonth_AcrossTheYear_TurnsTheYear", () => {
    expect(shiftMonth({ year: 1405, month: 1 }, -1)).toEqual({ year: 1404, month: 12 });
    expect(shiftMonth({ year: 1404, month: 12 }, 1)).toEqual({ year: 1405, month: 1 });
    expect(sameMonth(shiftMonth({ year: 1405, month: 8 }, 0), { year: 1405, month: 8 })).toBe(true);
  });

  it("Format_MonthAndClock_InPersianDigits", () => {
    expect(formatMonth({ year: 1405, month: 8 })).toBe("آبان ۱۴۰۵");
    expect(formatClock("13:10:00")).toBe("۱۳:۱۰");
  });
});
