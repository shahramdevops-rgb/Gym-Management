import { isPositiveMoney, moneyDigits, moneyOrNull, normalizeMoney, subtractMoney } from "./money";

describe("normalizeMoney", () => {
  it.each([
    ["1500000", "1500000"],
    ["۱۵۰۰۰۰۰", "1500000"],
    ["1,500,000", "1500000"],
    ["۱٬۵۰۰٬۰۰۰", "1500000"],
    ["۱٬۵۰۰٬۰۰۰٫۵۰", "1500000.50"],
    [" 1 500 000 ", "1500000"],
    ["", ""],
  ])("normalizeMoney_%j_Returns%j", (text, expected) => {
    expect(normalizeMoney(text)).toBe(expected);
  });

  it("normalizeMoney_NotANumber_LeavesItAloneForTheValidator", () => {
    // Stripped instead of kept, "۹۰۰ تومان" would pass as 900 rather than be refused.
    expect(normalizeMoney("۹۰۰ تومان")).toBe("900تومان");
  });

  it("normalizeMoney_LargestAmount_KeepsEveryDigitAsAString", () => {
    // The point of the string: this amount cannot survive a round trip through a double.
    expect(Number("9999999999999999.99")).toBe(10000000000000000);
    expect(normalizeMoney("۹٬۹۹۹٬۹۹۹٬۹۹۹٬۹۹۹٬۹۹۹٫۹۹")).toBe("9999999999999999.99");
  });
});

describe("moneyDigits", () => {
  it.each([
    ["۱۲۳", "123"],
    ["۱٬۲۳۴", "1234"],
    ["12abc34", "1234"],
    ["1200.", "1200."],
    ["1200٫5", "1200.5"],
    ["", ""],
  ])("moneyDigits_%j_Returns%j", (text, expected) => {
    expect(moneyDigits(text)).toBe(expected);
  });

  it("moneyDigits_SecondPoint_DropsIt", () => {
    expect(moneyDigits("1.2.3")).toBe("1.23");
  });
});

describe("moneyOrNull", () => {
  it("moneyOrNull_Empty_ReturnsNull", () => {
    expect(moneyOrNull("")).toBeNull();
    expect(moneyOrNull("   ")).toBeNull();
  });

  it("moneyOrNull_Amount_ReturnsTheNormalizedAmount", () => {
    expect(moneyOrNull("۹۰۰٬۰۰۰")).toBe("900000");
  });
});

describe("isPositiveMoney", () => {
  it.each(["1", "0.01", "900000", "۹۰۰٬۰۰۰", 1500000])(
    "isPositiveMoney_AboveZero %j_ReturnsTrue",
    (value) => {
      expect(isPositiveMoney(value)).toBe(true);
    },
  );

  it.each(["0", "0.00", "", "-5", "abc", 0, null, undefined])(
    "isPositiveMoney_ZeroOrNotAnAmount %j_ReturnsFalse",
    (value) => {
      expect(isPositiveMoney(value)).toBe(false);
    },
  );
});

describe("subtractMoney", () => {
  it.each([
    ["1500000", "500000", "1000000.00"],
    ["1500000", "1500000", "0.00"],
    ["500000", "1500000", "-1000000.00"],
    ["0.30", "0.10", "0.20"],
    ["1", "0.01", "0.99"],
  ])("subtractMoney_%j_Minus_%j_Returns%j", (left, right, expected) => {
    expect(subtractMoney(left, right)).toBe(expected);
  });

  it("subtractMoney_LargestAmounts_StaysExact", () => {
    // 9,999,999,999,999,999.99 − 0.99. A double would answer 10000000000000000.
    expect(subtractMoney("9999999999999999.99", "0.99")).toBe("9999999999999999.00");
  });

  it.each([
    ["abc", "1"],
    ["1", null],
    [null, "1"],
  ])("subtractMoney_NotAnAmount %j %j_ReturnsEmpty", (left, right) => {
    expect(subtractMoney(left, right)).toBe("");
  });
});
