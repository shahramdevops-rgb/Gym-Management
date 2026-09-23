import { amountProblem } from "./schemas";

describe("payment schemas", () => {
  it.each(["1", "900000", "1500000.5", "1500000.50", "9999999999999999.99", "۹۰۰٬۰۰۰"])(
    "amountProblem_Valid %j_ReturnsNull",
    (text) => {
      expect(amountProblem(text)).toBeNull();
    },
  );

  it.each([
    ["", "مبلغ را وارد کنید."],
    // Unlike a plan's price, zero is refused: a payment amount must be greater than zero.
    ["0", "مبلغ باید بزرگ‌تر از صفر باشد."],
    ["-5", "مبلغ باید بزرگ‌تر از صفر باشد."],
    ["12abc", "مبلغ باید یک عدد باشد."],
    ["10.001", "مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد."],
    ["10000000000000000", "مبلغ بیش از حد بزرگ است."],
  ])("amountProblem_Invalid %j_ReturnsTheReason", (text, expected) => {
    expect(amountProblem(text)).toBe(expected);
  });
});
