import { cancelChequeSchema, chequeAmountProblem, chequeSchema, isDue } from "./schemas";

const valid = { amount: "50000000", dueDate: "2026-11-01", payee: "فروشگاه", description: "قسط" };

describe("chequeAmountProblem", () => {
  it.each([
    ["", "مبلغ را وارد کنید."],
    ["0", "مبلغ باید بزرگ‌تر از صفر باشد."],
    ["۰٫۰۰", "مبلغ باید بزرگ‌تر از صفر باشد."],
    ["-5", "مبلغ باید بزرگ‌تر از صفر باشد."],
    ["1000.001", "مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد."],
    ["12345678901234567", "مبلغ بیش از حد بزرگ است."],
    ["abc", "مبلغ باید یک عدد باشد."],
  ])("chequeAmountProblem_%s_IsRefused", (text, expected) => {
    expect(chequeAmountProblem(text)).toBe(expected);
  });

  it.each(["1", "۵۰٬۰۰۰٬۰۰۰", "1234567890123456.99"])(
    "chequeAmountProblem_%s_IsAccepted",
    (text) => {
      expect(chequeAmountProblem(text)).toBeNull();
    },
  );
});

describe("chequeSchema", () => {
  it("chequeSchema_PastDate_IsAccepted", () => {
    // §9 Cheques: an old cheque can be entered late.
    expect(chequeSchema.safeParse({ ...valid, dueDate: "2020-01-01" }).success).toBe(true);
  });

  it("chequeSchema_PayeeAtTheLimit_IsAcceptedAndOneMoreIsRefused", () => {
    expect(chequeSchema.safeParse({ ...valid, payee: "ب".repeat(200) }).success).toBe(true);
    expect(chequeSchema.safeParse({ ...valid, payee: "ب".repeat(201) }).success).toBe(false);
  });

  it("chequeSchema_BlankPayeeAndDescription_AreRefused", () => {
    const result = chequeSchema.safeParse({ ...valid, payee: "  ", description: " " });

    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.message)).toEqual([
      "نام گیرنده (در وجه) را وارد کنید.",
      "شرح چک را وارد کنید.",
    ]);
  });

  it("chequeSchema_DescriptionOverTheLimit_IsRefused", () => {
    expect(chequeSchema.safeParse({ ...valid, description: "ت".repeat(501) }).success).toBe(false);
  });
});

describe("cancelChequeSchema", () => {
  it("cancelChequeSchema_BlankReason_IsRefused", () => {
    expect(cancelChequeSchema.safeParse({ reason: "  " }).success).toBe(false);
  });

  it("cancelChequeSchema_ReasonAtTheLimit_IsAccepted", () => {
    expect(cancelChequeSchema.safeParse({ reason: "ر".repeat(500) }).success).toBe(true);
  });
});

describe("isDue", () => {
  it.each([
    ["2026-10-04", true],
    ["2026-10-05", true],
    ["2026-10-06", false],
  ])("isDue_%s_OnTheFifth_Is%s", (dueDate, expected) => {
    expect(isDue(dueDate, "2026-10-05")).toBe(expected);
  });
});
