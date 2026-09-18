import { normalizeDigits, normalizeInput, normalizePersianText } from "./normalize";

// Look-alike and invisible characters are built from escapes so the test says exactly which
// code point it feeds in; a literal "ي" and "ی" are indistinguishable on screen.
const arabicYeh = "\u064A";
const alefMaksura = "\u0649";
const arabicKaf = "\u0643";
const persianYeh = "\u06CC";
const persianKeheh = "\u06A9";
const zwnj = "\u200C";
const zwj = "\u200D";
const rtlMark = "\u200F";
const tatweel = "\u0640";
const fatha = "\u064E";

describe("normalizeDigits", () => {
  it("normalizeDigits_PersianDigits_ReturnsEnglishDigits", () => {
    expect(normalizeDigits("۰۱۲۳۴۵۶۷۸۹")).toBe("0123456789");
  });

  it("normalizeDigits_ArabicDigits_ReturnsEnglishDigits", () => {
    expect(normalizeDigits("٠١٢٣٤٥٦٧٨٩")).toBe("0123456789");
  });

  it("normalizeDigits_MixedDigitsAndText_ConvertsOnlyDigits", () => {
    expect(normalizeDigits("کد ۱2٣")).toBe("کد 123");
  });

  it("normalizeDigits_EnglishText_ReturnsItUnchanged", () => {
    expect(normalizeDigits("abc 123")).toBe("abc 123");
  });
});

describe("normalizePersianText", () => {
  it("normalizePersianText_ArabicYehAndAlefMaksura_ReturnsPersianYeh", () => {
    expect(normalizePersianText(`عل${arabicYeh}`)).toBe(`عل${persianYeh}`);
    expect(normalizePersianText(`موس${alefMaksura}`)).toBe(`موس${persianYeh}`);
  });

  it("normalizePersianText_ArabicKaf_ReturnsPersianKeheh", () => {
    expect(normalizePersianText(`${arabicKaf}ر${arabicYeh}م`)).toBe(
      `${persianKeheh}ر${persianYeh}م`,
    );
  });

  it("normalizePersianText_Zwnj_BecomesASpace", () => {
    expect(normalizePersianText(`میر${zwnj}حسین`)).toBe("میر حسین");
  });

  it("normalizePersianText_HalfSpaceAndSpace_ProduceTheSameString", () => {
    expect(normalizePersianText(`می${zwnj}روم`)).toBe(normalizePersianText("می روم"));
  });

  it("normalizePersianText_ZwnjBesideSpaces_CollapsesToOneSpace", () => {
    expect(normalizePersianText(`میر ${zwnj} حسین`)).toBe("میر حسین");
  });

  it("normalizePersianText_HarakatAndTatweel_AreRemoved", () => {
    expect(normalizePersianText(`م${fatha}حمـ${tatweel}د`)).toBe("محمد");
  });

  it("normalizePersianText_OtherZeroWidthAndDirectionMarks_AreRemoved", () => {
    expect(normalizePersianText(`${rtlMark}رضا${zwj}`)).toBe("رضا");
  });

  it("normalizePersianText_RepeatedAndOuterWhitespace_IsCollapsedAndTrimmed", () => {
    expect(normalizePersianText("  علی \t  رضایی \n ")).toBe("علی رضایی");
  });

  it("normalizePersianText_EmptyString_ReturnsEmptyString", () => {
    expect(normalizePersianText("   ")).toBe("");
  });
});

describe("normalizeInput", () => {
  it("normalizeInput_PastedPhoneWithMixedDigits_ReturnsEnglishDigits", () => {
    expect(normalizeInput(" ۰۹۱۲ ٣٤٥ ۶۷۸۹ ")).toBe("0912 345 6789");
  });

  it("normalizeInput_ArabicKeyboardName_MatchesPersianKeyboardName", () => {
    expect(normalizeInput(`عل${arabicYeh}  ${arabicKaf}ر${arabicYeh}م${zwnj}`)).toBe(
      normalizeInput("علی کریم"),
    );
  });
});
