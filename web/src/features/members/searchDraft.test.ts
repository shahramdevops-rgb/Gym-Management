import { memberDraftFromSearch } from "./searchDraft";

describe("memberDraftFromSearch", () => {
  it("MemberDraftFromSearch_Name_FillsTheNameOnly", () => {
    const draft = memberDraftFromSearch("  سارا  محمدي ");

    expect(draft.fullName).toBe("سارا محمدی");
    expect(draft.phoneNumber).toBe("");
  });

  it("MemberDraftFromSearch_PersianDigits_FillsThePhoneAsTyped", () => {
    const draft = memberDraftFromSearch("۰۹۱۲ ۱۲۳ ۴۵۶۷");

    expect(draft.phoneNumber).toBe("۰۹۱۲ ۱۲۳ ۴۵۶۷");
    expect(draft.fullName).toBe("");
  });

  it("MemberDraftFromSearch_InternationalPhone_FillsThePhone", () => {
    expect(memberDraftFromSearch("+98 912-123-4567").phoneNumber).toBe("+98 912-123-4567");
  });

  it("MemberDraftFromSearch_PhoneWithBrackets_FillsThePhone", () => {
    expect(memberDraftFromSearch("(0912) 123 4567").phoneNumber).toBe("(0912) 123 4567");
  });

  it("MemberDraftFromSearch_NameWithDigits_FillsTheName", () => {
    expect(memberDraftFromSearch("علی 2").fullName).toBe("علی 2");
  });
});
