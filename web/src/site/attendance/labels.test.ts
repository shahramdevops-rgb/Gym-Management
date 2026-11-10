import {
  formatCheckOut,
  formatClock,
  formatEarly,
  formatLate,
  formatMinutes,
  formatMonth,
  formatVisit,
  monthOf,
  monthRange,
  sameMonth,
  shiftDay,
  shiftMonth,
} from "./labels";

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

  it("FormatCheckOut_EachCase_ReadsAsTheTrainerSeesIt", () => {
    expect(formatCheckOut("14:40:00", false)).toBe("خروج ۱۴:۴۰");
    expect(formatCheckOut(null, false)).toBe("داخل است");
    expect(formatCheckOut(null, true)).toBe("خروج ثبت نشد");
  });

  it("FormatVisit_CheckInAndOut_OnOneLine", () => {
    expect(
      formatVisit({ checkedInAt: "18:00:00", checkedOutAt: "19:30:00", autoCheckedOut: false }),
    ).toBe("ورود ۱۸:۰۰ · خروج ۱۹:۳۰");
  });

  it("ShiftDay_AcrossAMonthAndAYear_TurnsThem", () => {
    expect(shiftDay("2026-11-01", -1)).toBe("2026-10-31");
    expect(shiftDay("2026-12-31", 1)).toBe("2027-01-01");
    expect(shiftDay("2026-03-21", 0)).toBe("2026-03-21");
  });

  it("FormatMinutes_UnderAndOverAnHour_ReadNaturally", () => {
    expect(formatMinutes(25)).toBe("۲۵ دقیقه");
    expect(formatMinutes(60)).toBe("۱ ساعت");
    expect(formatMinutes(125)).toBe("۲ ساعت و ۵ دقیقه");
  });

  it("FormatEarlyAndLate_SayHowMuch", () => {
    expect(formatEarly(10)).toBe("۱۰ دقیقه زودتر آمد");
    expect(formatLate(25)).toBe("۲۵ دقیقه تأخیر");
  });
});
