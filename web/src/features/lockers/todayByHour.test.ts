import { gymHour, visibleHours, type HourCount } from "./todayByHour";

/** 24 empty hours with the given ones filled in. */
function day(filled: Partial<Record<number, Partial<HourCount>>>): HourCount[] {
  return Array.from({ length: 24 }, (_, hour) => ({ hour, today: 0, average: 0, ...filled[hour] }));
}

describe("visibleHours", () => {
  it("VisibleHours_NothingAnywhere_IsEmpty", () => {
    expect(visibleHours(day({}))).toEqual([]);
  });

  it("VisibleHours_TodayAndAverage_RunsFromTheFirstToTheLastBusyHourWithTheGapsKept", () => {
    const shown = visibleHours(day({ 7: { average: 1.5 }, 9: { today: 2 }, 21: { today: 1 } }));

    expect(shown.map((row) => row.hour)).toEqual([
      7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21,
    ]);
  });

  it("VisibleHours_OnlyAnAverageLater_KeepsTheHoursStillToCome", () => {
    const shown = visibleHours(day({ 8: { today: 3 }, 22: { average: 0.3 } }));

    expect(shown[0]?.hour).toBe(8);
    expect(shown.at(-1)?.hour).toBe(22);
  });
});

describe("gymHour", () => {
  it("GymHour_AMomentInUtc_IsTheHourOnTehransClock", () => {
    // 20:40 UTC is 00:10 the next day in Tehran (UTC+03:30), and 05:00 UTC is 08:30.
    expect(gymHour(new Date("2026-09-29T20:40:00Z"))).toBe(0);
    expect(gymHour(new Date("2026-09-30T05:00:00Z"))).toBe(8);
    expect(gymHour(new Date("2026-09-30T20:29:00Z"))).toBe(23);
  });
});
