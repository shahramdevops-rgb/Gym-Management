import {
  emptyValue,
  formatDate,
  formatDateTime,
  formatMoney,
  formatNumber,
  formatPhone,
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

describe("formatMoney", () => {
  it("formatMoney_Amount_AppendsToman", () => {
    expect(formatMoney(1250000)).toBe("۱٬۲۵۰٬۰۰۰ تومان");
  });

  it("formatMoney_Missing_ReturnsEmptyValueWithoutUnit", () => {
    expect(formatMoney(null)).toBe(emptyValue);
  });
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
