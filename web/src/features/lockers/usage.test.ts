import { usageLevel } from "./usage";

describe("usageLevel", () => {
  it("usageLevel_NeverUsed_IsZero", () => {
    expect(usageLevel(0, 40)).toBe(0);
  });

  it("usageLevel_NothingUsedAtAll_IsZero", () => {
    expect(usageLevel(0, 0)).toBe(0);
  });

  it("usageLevel_MostUsed_IsTheDarkest", () => {
    expect(usageLevel(40, 40)).toBe(5);
  });

  it("usageLevel_UsedOnceBesideABusyLocker_IsStillAtLeastOne", () => {
    expect(usageLevel(1, 100)).toBe(1);
  });

  it("usageLevel_InBetween_StepsUpInFifths", () => {
    expect(usageLevel(8, 40)).toBe(1);
    expect(usageLevel(9, 40)).toBe(2);
    expect(usageLevel(20, 40)).toBe(3);
    expect(usageLevel(33, 40)).toBe(5);
  });
});
