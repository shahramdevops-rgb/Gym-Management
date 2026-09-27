import { expenseAmountProblem } from "./schemas";

describe("expenseAmountProblem", () => {
  it.each(["۵۰۰٬۰۰۰", "500,000", "0.5", "1200000.25", "9999999999999999.99"])(
    "ExpenseAmountProblem_ValidAmount_%s_ReturnsNull",
    (text) => {
      expect(expenseAmountProblem(text)).toBeNull();
    },
  );

  it.each(["0", "0.00", "000", "-5"])(
    "ExpenseAmountProblem_ZeroOrNegative_%s_IsRefused",
    (text) => {
      // BUSINESS_RULES.md §9: an expense is greater than zero.
      expect(expenseAmountProblem(text)).toBe("مبلغ باید بزرگ‌تر از صفر باشد.");
    },
  );

  it("ExpenseAmountProblem_ThreeDecimals_IsRefusedRatherThanRounded", () => {
    expect(expenseAmountProblem("1000.125")).toBe("مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد.");
  });

  it("ExpenseAmountProblem_SeventeenDigits_IsTooLarge", () => {
    expect(expenseAmountProblem("10000000000000000")).toBe("مبلغ بیش از حد بزرگ است.");
  });

  it("ExpenseAmountProblem_Blank_AsksForAnAmount", () => {
    expect(expenseAmountProblem("  ")).toBe("مبلغ را وارد کنید.");
  });
});
