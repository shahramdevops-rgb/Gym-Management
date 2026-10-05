import {
  busyHours,
  byJalaliMonth,
  dailyChartMaxDays,
  outcomeOf,
  percentChange,
  renewalRate,
  revenueSeries,
} from "./figures";
import { isoDaysBefore } from "@/features/history/range";

describe("outcomeOf", () => {
  it.each([
    [7000000, "gain"],
    ["1500000.00", "gain"],
    [-30500000, "loss"],
    ["-0.50", "loss"],
    [0, undefined],
    ["0.00", undefined],
    ["abc", undefined],
  ] as const)("outcomeOf_%j_Is%j", (value, expected) => {
    expect(outcomeOf(value)).toBe(expected);
  });
});

describe("percentChange", () => {
  it("percentChange_Rise_IsPositive", () => {
    expect(percentChange(120, 100)).toBe(20);
  });

  it("percentChange_Fall_IsNegative", () => {
    expect(percentChange("75000.00", "100000.00")).toBe(-25);
  });

  it("percentChange_NothingBefore_IsNull", () => {
    expect(percentChange(500, 0)).toBeNull();
  });

  it("percentChange_LossBefore_IsNull", () => {
    // From −100 to 50 is not "−150٪".
    expect(percentChange(50, -100)).toBeNull();
  });
});

describe("renewalRate", () => {
  it("renewalRate_WaitingLeftOut_RenewedOverDecided", () => {
    // 5 ended, 1 still waiting: 3 of the 4 decided were renewed.
    expect(renewalRate(3, 5, 1)).toBe(75);
  });

  it("renewalRate_AllWaiting_IsNull", () => {
    expect(renewalRate(0, 2, 2)).toBeNull();
  });

  it("renewalRate_NothingEnded_IsNull", () => {
    expect(renewalRate(0, 0, 0)).toBeNull();
  });
});

describe("byJalaliMonth", () => {
  it("byJalaliMonth_DaysEitherSideOfTheFirst_SplitIntoTwoMonths", () => {
    const months = byJalaliMonth([
      // ۱۴۰۵/۰۶/۳۱ and ۱۴۰۵/۰۷/۰۱
      { date: "2026-09-22", ended: 2, renewed: 1, waiting: 0, newMembers: 1 },
      { date: "2026-09-23", ended: 1, renewed: 0, waiting: 1, newMembers: 2 },
      { date: "2026-09-24", ended: 3, renewed: 3, waiting: 0, newMembers: 0 },
    ]);

    expect(months).toEqual([
      {
        key: "1405/06",
        label: "شهریور ۱۴۰۵",
        ended: 2,
        renewed: 1,
        waiting: 0,
        newMembers: 1,
        renewalRate: 50,
      },
      {
        key: "1405/07",
        label: "مهر ۱۴۰۵",
        ended: 4,
        renewed: 3,
        waiting: 1,
        newMembers: 2,
        renewalRate: 100,
      },
    ]);
  });

  it("byJalaliMonth_AcrossNowruz_KeepsTheYearsInOrder", () => {
    const months = byJalaliMonth([
      { date: "2026-03-21", ended: 0, renewed: 0, waiting: 0, newMembers: 1 },
      { date: "2026-03-20", ended: 0, renewed: 0, waiting: 0, newMembers: 1 },
    ]);

    expect(months.map((month) => month.label)).toEqual(["اسفند ۱۴۰۴", "فروردین ۱۴۰۵"]);
  });
});

describe("revenueSeries", () => {
  function days(count: number) {
    return Array.from({ length: count }, (_, index) => ({
      date: isoDaysBefore("2026-10-04", count - 1 - index),
      revenue: "1000.00",
      expenses: 500,
    }));
  }

  it("revenueSeries_UpToTwoMonths_IsDayByDay", () => {
    const points = revenueSeries(days(dailyChartMaxDays));

    expect(points).toHaveLength(dailyChartMaxDays);
    expect(points.at(-1)).toEqual({
      key: "2026-10-04",
      label: "۷/۱۲",
      title: "۱۲ مهر ۱۴۰۵",
      revenue: 1000,
      expenses: 500,
    });
  });

  it("revenueSeries_LongerRange_AddsUpJalaliMonths", () => {
    // 63 days back from ۱۴۰۵/۰۷/۱۲: the end of مرداد, all of شهریور, and مهر so far.
    const points = revenueSeries(days(dailyChartMaxDays + 1));

    expect(points.map((point) => point.label)).toEqual(["مرداد ۱۴۰۵", "شهریور ۱۴۰۵", "مهر ۱۴۰۵"]);
    expect(points.at(-1)).toMatchObject({ revenue: 12000, expenses: 6000 });
  });
});

describe("busyHours", () => {
  function week(visitsAt: number[]) {
    return [
      { hours: Array.from({ length: 24 }, (_, hour) => (visitsAt.includes(hour) ? 1 : 0)) },
      { hours: Array.from({ length: 24 }, () => "0") },
    ];
  }

  it("busyHours_Visits_RunFromTheEarliestToTheLatestHour", () => {
    expect(busyHours(week([7, 9, 21]))).toEqual([
      7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21,
    ]);
  });

  it("busyHours_NoVisits_IsTheGymsUsualDay", () => {
    expect(busyHours(week([]))).toEqual(Array.from({ length: 18 }, (_, index) => 6 + index));
  });
});
