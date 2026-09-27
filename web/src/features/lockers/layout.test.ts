import { cabinetColumns, lockerZones, mapLockerNumbers } from "./layout";

describe("locker layout", () => {
  it("mapLockerNumbers_TheWholeMap_HasEveryLockerFrom1To72ExactlyOnce", () => {
    const numbers = mapLockerNumbers();

    expect([...numbers].sort((a, b) => a - b)).toEqual(Array.from({ length: 72 }, (_, i) => i + 1));
  });

  it("cabinetColumns_FirstCabinet_RunsDownAColumnThenOnToTheNext", () => {
    expect(cabinetColumns(1)).toEqual([
      [1, 2, 3],
      [4, 5, 6],
    ]);
  });

  it("lockerZones_OutsideAndInside_SplitAt30", () => {
    const [outside, inside] = lockerZones;

    const numbersOf = (zone: (typeof lockerZones)[number]) =>
      zone.groups.flatMap((group) =>
        group.cabinets.flatMap((first) => cabinetColumns(first).flat()),
      );
    expect(outside!.name).toBe("بیرون رختکن");
    expect(Math.max(...numbersOf(outside!))).toBe(30);
    expect(inside!.name).toBe("داخل رختکن");
    expect(Math.min(...numbersOf(inside!))).toBe(31);
  });

  it("lockerZones_Inside_DrawsTheFreeStandingCabinetApartFromTheWall", () => {
    const inside = lockerZones[1]!;

    expect(inside.groups.map((group) => group.cabinets)).toEqual([[31, 37, 43, 49, 55, 61], [67]]);
  });
});
