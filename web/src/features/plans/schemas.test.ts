import { parseWholeNumber, priceProblem } from "./schemas";

describe("plan schemas", () => {
  it.each([
    ["30", 30],
    ["۳۰", 30],
    ["٣٠", 30],
    [" 7 ", 7],
    ["", null],
    ["3.5", null],
    ["-1", null],
    ["سی", null],
  ])("parseWholeNumber_%j_Returns%j", (text, expected) => {
    expect(parseWholeNumber(text)).toBe(expected);
  });

  it.each(["0", "900000", "1500000.5", "1500000.50", "9999999999999999.99", "۱٬۵۰۰٬۰۰۰"])(
    "priceProblem_Valid %j_ReturnsNull",
    (text) => {
      expect(priceProblem(text)).toBeNull();
    },
  );

  it.each([
    ["", "قیمت را وارد کنید."],
    ["-5", "قیمت نمی‌تواند منفی باشد."],
    ["12abc", "قیمت باید یک عدد باشد."],
    ["12.", "قیمت باید یک عدد باشد."],
    // Refused, never rounded: the API would refuse it too (Plans.PriceTooManyDecimals).
    ["10.001", "قیمت حداکثر می‌تواند ۲ رقم اعشار داشته باشد."],
    ["10000000000000000", "قیمت بیش از حد بزرگ است."],
  ])("priceProblem_Invalid %j_ReturnsTheReason", (text, expected) => {
    expect(priceProblem(text)).toBe(expected);
  });

  it("priceProblem_LargestPrice_IsAccepted", () => {
    // How the separators come off is `normalizeMoney`'s job, tested in lib/money.test.ts; what
    // matters here is that the largest price the API allows is not refused on the way.
    expect(priceProblem("9,999,999,999,999,999.99")).toBeNull();
  });
});
