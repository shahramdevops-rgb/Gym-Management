import { stayProgress } from "./longStay";

// BUSINESS_RULES.md §6 *Long stay*: the bar fills over three hours and warns from three hours.

const checkedInAt = "2026-09-29T06:00:00Z";
const after = (minutes: number) => new Date(Date.parse(checkedInAt) + minutes * 60_000);

describe("stayProgress", () => {
  it("StayProgress_JustCheckedIn_IsEmptyAndNotLong", () => {
    expect(stayProgress(checkedInAt, after(0))).toEqual({ fraction: 0, isLong: false });
  });

  it("StayProgress_OneAndAHalfHours_IsHalfFull", () => {
    expect(stayProgress(checkedInAt, after(90))).toEqual({ fraction: 0.5, isLong: false });
  });

  it("StayProgress_AMinuteShortOfThreeHours_IsNotLongYet", () => {
    const progress = stayProgress(checkedInAt, after(179));
    expect(progress.isLong).toBe(false);
    expect(progress.fraction).toBeLessThan(1);
  });

  it("StayProgress_ExactlyThreeHours_IsFullAndLong", () => {
    expect(stayProgress(checkedInAt, after(180))).toEqual({ fraction: 1, isLong: true });
  });

  it("StayProgress_FiveHours_StaysFull", () => {
    expect(stayProgress(checkedInAt, after(300))).toEqual({ fraction: 1, isLong: true });
  });

  it("StayProgress_BrowserClockBehindTheServer_CountsAsJustStarted", () => {
    expect(stayProgress(checkedInAt, after(-2))).toEqual({ fraction: 0, isLong: false });
  });
});
