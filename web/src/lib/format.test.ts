import {
  amountInPersianWords,
  emptyValue,
  formatDate,
  formatDateTime,
  formatMoney,
  formatMoneyDigits,
  formatNumber,
  formatPhone,
  gymToday,
  isoYearsAgo,
  toIsoDate,
  toJalaliInput,
  toPersianDigits,
} from "./format";

describe("toPersianDigits", () => {
  it("toPersianDigits_EnglishDigits_ReturnsPersianDigits", () => {
    expect(toPersianDigits("0123456789")).toBe("۰۱۲۳۴۵۶۷۸۹");
  });

  it("toPersianDigits_Number_ReturnsPersianDigits", () => {
    expect(toPersianDigits(2026)).toBe("۲۰۲۶");
  });

  it("toPersianDigits_MixedText_LeavesNonDigitsAlone", () => {
    expect(toPersianDigits("جلسه 3 از 12")).toBe("جلسه ۳ از ۱۲");
  });
});

describe("formatNumber", () => {
  it("formatNumber_LargeNumber_UsesPersianDigitsAndSeparators", () => {
    expect(formatNumber(1250000)).toBe("۱٬۲۵۰٬۰۰۰");
  });

  it("formatNumber_Zero_ReturnsPersianZero", () => {
    expect(formatNumber(0)).toBe("۰");
  });

  it("formatNumber_Negative_KeepsTheSign", () => {
    expect(formatNumber(-3500)).toContain("۳٬۵۰۰");
    expect(formatNumber(-3500)).toMatch(/[-−]/);
  });

  it.each([null, undefined, Number.NaN, Number.POSITIVE_INFINITY])(
    "formatNumber_MissingOrNotFinite_ReturnsEmptyValue (%s)",
    (value) => {
      expect(formatNumber(value)).toBe(emptyValue);
    },
  );
});

describe("formatMoneyDigits", () => {
  it.each([
    ["1500000", "۱٬۵۰۰٬۰۰۰"],
    ["500", "۵۰۰"],
    ["0", "۰"],
    ["1500000.5", "۱٬۵۰۰٬۰۰۰٫۵"],
    ["", ""],
    ["abc", ""],
  ])("formatMoneyDigits_%j_Returns%j", (digits, expected) => {
    expect(formatMoneyDigits(digits)).toBe(expected);
  });

  it("formatMoneyDigits_HalfTypedDecimal_KeepsThePoint", () => {
    // The money box formats what is in it on every keystroke, so a point typed but not yet
    // followed by a digit has to survive the trip.
    expect(formatMoneyDigits("1500000.")).toBe("۱٬۵۰۰٬۰۰۰٫");
  });
});

describe("formatMoney", () => {
  it("formatMoney_Amount_AppendsToman", () => {
    expect(formatMoney(1250000)).toBe("۱٬۲۵۰٬۰۰۰ تومان");
  });

  it("formatMoney_DecimalString_FormatsItWithoutAFloat", () => {
    expect(formatMoney("1250000.50")).toBe("۱٬۲۵۰٬۰۰۰٫۵۰ تومان");
  });

  it("formatMoney_WholeAmountStoredWithTwoDecimals_DropsTheZeros", () => {
    expect(formatMoney("1500000.00")).toBe("۱٬۵۰۰٬۰۰۰ تومان");
  });

  it("formatMoney_LargestAmount_KeepsEveryDigit", () => {
    // Through a double this would read ۱۰٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰ تومان.
    expect(formatMoney("9999999999999999.99")).toBe("۹٬۹۹۹٬۹۹۹٬۹۹۹٬۹۹۹٬۹۹۹٫۹۹ تومان");
  });

  it.each([null, undefined, "", "abc"])(
    "formatMoney_MissingOrNotAnAmount_ReturnsEmptyValueWithoutUnit (%s)",
    (value) => {
      expect(formatMoney(value)).toBe(emptyValue);
    },
  );
});

describe("amountInPersianWords", () => {
  it("amountInPersianWords_Zero_ReturnsSefr", () => {
    expect(amountInPersianWords("0")).toBe("صفر تومان");
  });

  it.each([
    ["1", "یک تومان"],
    ["5", "پنج تومان"],
    ["9", "نه تومان"],
    ["10", "ده تومان"],
    ["15", "پانزده تومان"],
    ["21", "بیست و یک تومان"],
    ["100", "صد تومان"],
    ["115", "صد و پانزده تومان"],
    ["999", "نهصد و نود و نه تومان"],
  ])("amountInPersianWords_%j_Returns%j", (amount, expected) => {
    expect(amountInPersianWords(amount)).toBe(expected);
  });

  it("amountInPersianWords_OneThousand_SaysHezarWithoutYek", () => {
    // «یک هزار تومان» is not how anybody says it, while «یک میلیون تومان» is.
    expect(amountInPersianWords("1000")).toBe("هزار تومان");
  });

  it("amountInPersianWords_FiveHundredThousand_ReturnsThePhraseFromTheRules", () => {
    expect(amountInPersianWords("500000")).toBe("پانصد هزار تومان");
  });

  it.each([
    ["1000000", "یک میلیون تومان"],
    ["2500000", "دو میلیون و پانصد هزار تومان"],
    ["1234567", "یک میلیون و دویست و سی و چهار هزار و پانصد و شصت و هفت تومان"],
  ])("amountInPersianWords_%j_Returns%j", (amount, expected) => {
    expect(amountInPersianWords(amount)).toBe(expected);
  });

  it("amountInPersianWords_BillionBoundary_CrossesFromMillionsToMilliard", () => {
    expect(amountInPersianWords("999000000")).toBe("نهصد و نود و نه میلیون تومان");
    expect(amountInPersianWords("1000000000")).toBe("یک میلیارد تومان");
    expect(amountInPersianWords("1000000001")).toBe("یک میلیارد و یک تومان");
  });

  it("amountInPersianWords_LargestAmount_HasAScaleForEveryGroup", () => {
    expect(amountInPersianWords("1000000000000")).toBe("هزار میلیارد تومان");
    expect(amountInPersianWords("1000000000000000")).toBe("یک میلیون میلیارد تومان");
  });

  it("amountInPersianWords_TypedWithPersianDigitsAndSeparators_ReadsTheSame", () => {
    // This is the form the money field holds, so the words are computed from what is on screen.
    expect(amountInPersianWords("۹۰۰٬۰۰۰")).toBe("نهصد هزار تومان");
  });

  it.each([
    ["0.5", "پنجاه صدم تومان"],
    ["0.05", "پنج صدم تومان"],
    ["1500000.25", "یک میلیون و پانصد هزار و بیست و پنج صدم تومان"],
  ])("amountInPersianWords_Fraction %j_Returns%j", (amount, expected) => {
    expect(amountInPersianWords(amount)).toBe(expected);
  });

  it("amountInPersianWords_WholeAmountStoredWithTwoDecimals_IgnoresTheZeros", () => {
    expect(amountInPersianWords("500000.00")).toBe("پانصد هزار تومان");
  });

  it.each([null, undefined, "", "abc", "۹۰۰ تومان", "1.234", "1234567890123456789"])(
    "amountInPersianWords_NotAnAmount_ReturnsNothing (%s)",
    (value) => {
      // Nothing rather than a guess: a line that is not the amount is worse than no line.
      expect(amountInPersianWords(value)).toBe("");
    },
  );
});

describe("formatDate", () => {
  it("formatDate_IsoDate_ReturnsJalaliDate", () => {
    expect(formatDate("2026-09-17")).toBe("۱۴۰۵/۰۶/۲۶");
  });

  it("formatDate_Nowruz_StartsANewJalaliYear", () => {
    expect(formatDate("2026-03-20")).toBe("۱۴۰۴/۱۲/۲۹");
    expect(formatDate("2026-03-21")).toBe("۱۴۰۵/۰۱/۰۱");
  });

  it("formatDate_GregorianNewYear_IsMidJalaliYear", () => {
    expect(formatDate("2026-01-01")).toBe("۱۴۰۴/۱۰/۱۱");
  });

  it.each([null, undefined, "", "not a date", "2026-13-45", "2026-09-17T10:00:00Z"])(
    "formatDate_NotAnIsoDate_ReturnsEmptyValue (%s)",
    (value) => {
      expect(formatDate(value)).toBe(emptyValue);
    },
  );
});

describe("formatDateTime", () => {
  it("formatDateTime_UtcTimestamp_ShowsTehranLocalTime", () => {
    expect(formatDateTime("2026-09-17T08:15:00Z")).toBe("۱۴۰۵/۰۶/۲۶, ۱۱:۴۵");
  });

  it("formatDateTime_LateUtcEvening_IsAlreadyTomorrowInTehran", () => {
    // 21:00 UTC is 00:30 the next day at +03:30: the Jalali date must move with it.
    expect(formatDateTime("2026-09-17T21:00:00Z")).toBe("۱۴۰۵/۰۶/۲۷, ۰۰:۳۰");
  });

  it.each([null, undefined, "", "yesterday"])(
    "formatDateTime_MissingOrInvalid_ReturnsEmptyValue (%s)",
    (value) => {
      expect(formatDateTime(value)).toBe(emptyValue);
    },
  );
});

describe("formatPhone", () => {
  it("formatPhone_IranianMobileInE164_ShowsTheLocalFormatWithPersianDigits", () => {
    expect(formatPhone("+989121234567")).toBe("۰۹۱۲ ۱۲۳ ۴۵۶۷");
  });

  it("formatPhone_OtherNumber_ShowsItAsIsWithPersianDigits", () => {
    expect(formatPhone("+442071234567")).toBe("+۴۴۲۰۷۱۲۳۴۵۶۷");
  });

  it.each([null, undefined, ""])("formatPhone_Missing_ReturnsTheEmptyMarker (%s)", (value) => {
    expect(formatPhone(value)).toBe(emptyValue);
  });
});

describe("toIsoDate", () => {
  it("toIsoDate_JalaliDateWithPersianDigits_ReturnsTheIsoDate", () => {
    expect(toIsoDate("۱۳۷۰/۰۵/۱۲")).toBe("1991-08-03");
  });

  it("toIsoDate_EnglishDigits_ReturnsTheSameDate", () => {
    expect(toIsoDate("1370/05/12")).toBe("1991-08-03");
  });

  it("toIsoDate_ArabicDigits_ReturnsTheSameDate", () => {
    expect(toIsoDate("١٣٧٠/٠٥/١٢")).toBe("1991-08-03");
  });

  it("toIsoDate_WithoutLeadingZeros_ReturnsTheSameDate", () => {
    expect(toIsoDate("۱۳۷۰/۵/۱۲")).toBe("1991-08-03");
  });

  it("toIsoDate_Nowruz_IsTheTwentyFirstOfMarch", () => {
    expect(toIsoDate("۱۴۰۵/۰۱/۰۱")).toBe("2026-03-21");
  });

  it("toIsoDate_LastDayOfALeapJalaliYear_Exists", () => {
    // 1403 is a leap Jalali year, so it has a 30th of Esfand.
    expect(toIsoDate("۱۴۰۳/۱۲/۳۰")).toBe("2025-03-20");
  });

  it("toIsoDate_ThirtiethOfEsfandInACommonYear_ReturnsNull", () => {
    // 1404 has no 30th of Esfand. date-fns-jalali would roll it into the next year; a date
    // nobody picked must never be what gets saved.
    expect(toIsoDate("۱۴۰۴/۱۲/۳۰")).toBeNull();
  });

  it.each(["", "۱۳۷۰/۰۵", "۱۳۷۰/۱۳/۰۱", "۱۳۷۰/۰۵/۳۲", "۱۳۷۰/۰۰/۱۲", "abc", null, undefined])(
    "toIsoDate_NotAWholeJalaliDate_ReturnsNull (%s)",
    (value) => {
      expect(toIsoDate(value)).toBeNull();
    },
  );
});

describe("toJalaliInput", () => {
  it("toJalaliInput_IsoDate_ReturnsTheJalaliDateWithPersianDigits", () => {
    expect(toJalaliInput("1991-08-03")).toBe("۱۳۷۰/۰۵/۱۲");
  });

  it("toJalaliInput_RoundTripsWithToIsoDate", () => {
    expect(toIsoDate(toJalaliInput("1991-08-03"))).toBe("1991-08-03");
  });

  it("toJalaliInput_AgreesWithFormatDate", () => {
    // Two Jalali implementations now run in this app: ICU's calendar behind formatDate (the
    // profile page) and date-fns-jalali behind toJalaliInput (the edit box). If a Node or ICU
    // version ever made them disagree, a member's profile and their edit form would show
    // different birth dates. This says so here instead of on someone's screen.
    for (const iso of ["1991-08-03", "2026-03-20", "2026-03-21", "2025-03-20", "1900-01-01"]) {
      expect(toJalaliInput(iso)).toBe(formatDate(iso));
    }
  });

  it.each([null, undefined, "", "not-a-date", "1991-8-3"])(
    "toJalaliInput_NotAnIsoDate_ReturnsEmpty (%s)",
    (value) => {
      expect(toJalaliInput(value)).toBe("");
    },
  );
});

describe("gymToday", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("gymToday_LateUtcEvening_IsAlreadyTomorrowInTehran", () => {
    // 21:00 UTC is 00:30 the next day in Tehran (+03:30).
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-17T21:00:00Z"));

    expect(gymToday()).toBe("2026-09-18");
  });

  it("gymToday_EarlyUtcMorning_IsStillTheSameDayInTehran", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-17T06:00:00Z"));

    expect(gymToday()).toBe("2026-09-17");
  });
});

describe("isoYearsAgo", () => {
  it("isoYearsAgo_OrdinaryDate_SubtractsWholeYears", () => {
    expect(isoYearsAgo("2026-09-22", 120)).toBe("1906-09-22");
  });

  it("isoYearsAgo_LeapDayIntoACommonYear_ClampsToTheTwentyEighth", () => {
    // 1900 is not a leap year (the century rule). DateOnly.AddYears clamps the same way.
    expect(isoYearsAgo("2020-02-29", 120)).toBe("1900-02-28");
  });

  it("isoYearsAgo_LeapDayIntoALeapYear_KeepsTheTwentyNinth", () => {
    expect(isoYearsAgo("2028-02-29", 120)).toBe("1908-02-29");
  });
});
