import { matchesStudent } from "./studentSearch";

const ali = { fullName: "علی محمدی", phoneNumber: "+989121234567" };
const mirhossein = { fullName: "میر‌حسین کاظمی", phoneNumber: "+989357654321" };

describe("matchesStudent (15.7d)", () => {
  it("MatchesStudent_EmptySearch_MatchesEveryone", () => {
    expect(matchesStudent(ali, "")).toBe(true);
    expect(matchesStudent(ali, "   ")).toBe(true);
  });

  it("MatchesStudent_PartOfTheName_Matches", () => {
    expect(matchesStudent(ali, "محمد")).toBe(true);
    expect(matchesStudent(ali, "رضا")).toBe(false);
  });

  it("MatchesStudent_ArabicLettersAndHalfSpace_MatchThePersianName", () => {
    expect(matchesStudent(ali, "علي")).toBe(true);
    expect(matchesStudent(mirhossein, "میر حسین")).toBe(true);
  });

  it("MatchesStudent_PhoneWrittenAnyWay_Matches", () => {
    expect(matchesStudent(ali, "0912 123")).toBe(true);
    expect(matchesStudent(ali, "۴۵۶۷")).toBe(true);
    expect(matchesStudent(ali, "+98 912 123 4567")).toBe(true);
    expect(matchesStudent(ali, "9121234567")).toBe(true);
    expect(matchesStudent(ali, "0935")).toBe(false);
  });

  it("MatchesStudent_OnlyZeros_MatchesNoOne", () => {
    expect(matchesStudent(ali, "0")).toBe(false);
  });
});
