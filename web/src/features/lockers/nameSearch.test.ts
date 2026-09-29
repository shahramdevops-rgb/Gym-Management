import { nameMatches, nameSearchTerm } from "./nameSearch";

describe("nameSearchTerm", () => {
  it.each([
    ["", null],
    [" ر ", null],
    ["رضا", "رضا"],
    ["  علي  ", "علی"],
  ])("nameSearchTerm_%j_Returns%j", (text, expected) => {
    expect(nameSearchTerm(text)).toBe(expected);
  });
});

describe("nameMatches", () => {
  it("nameMatches_PartOfTheLastName_IsAMatch", () => {
    expect(nameMatches("علی رضایی", "رضای")).toBe(true);
  });

  it("nameMatches_ArabicKafInTheStoredName_MatchesThePersianTerm", () => {
    expect(nameMatches("كامران", nameSearchTerm("کامران")!)).toBe(true);
  });

  it("nameMatches_HalfSpaceInTheName_MatchesASpaceInTheTerm", () => {
    expect(nameMatches("میر\u200Cحسین", nameSearchTerm("میر حسین")!)).toBe(true);
  });

  it("nameMatches_AnotherName_IsNotAMatch", () => {
    expect(nameMatches("رضا احمدی", "کریمی")).toBe(false);
  });
});
