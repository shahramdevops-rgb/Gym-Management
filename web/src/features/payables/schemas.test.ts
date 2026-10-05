import {
  canPayToday,
  cancelPayableSchema,
  payableAmountProblem,
  payableSchema,
  revertPayableSchema,
  wholeNumber,
} from "./schemas";

const cheque = {
  kind: "Cheque" as const,
  amount: "50000000",
  dueDate: "2026-11-01",
  payee: "فروشگاه",
  description: "تردمیل",
  categoryId: "0199a000-0000-7000-8000-0000000000a3",
  installmentNumber: "",
  installmentCount: "",
};

const installment = {
  ...cheque,
  kind: "Installment" as const,
  installmentNumber: "3",
  installmentCount: "12",
};

function messages(values: object) {
  const result = payableSchema.safeParse(values);
  return result.success ? [] : result.error.issues.map((issue) => issue.message);
}

describe("payableAmountProblem", () => {
  it.each([
    ["", "مبلغ را وارد کنید."],
    ["0", "مبلغ باید بزرگ‌تر از صفر باشد."],
    ["۰٫۰۰", "مبلغ باید بزرگ‌تر از صفر باشد."],
    ["-5", "مبلغ باید بزرگ‌تر از صفر باشد."],
    ["1000.001", "مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد."],
    ["12345678901234567", "مبلغ بیش از حد بزرگ است."],
    ["abc", "مبلغ باید یک عدد باشد."],
  ])("payableAmountProblem_%s_IsRefused", (text, expected) => {
    expect(payableAmountProblem(text)).toBe(expected);
  });

  it.each(["1", "۵۰٬۰۰۰٬۰۰۰", "1234567890123456.99"])(
    "payableAmountProblem_%s_IsAccepted",
    (text) => {
      expect(payableAmountProblem(text)).toBeNull();
    },
  );
});

describe("wholeNumber", () => {
  it.each([
    ["12", 12],
    ["۱۲", 12],
    [" 3 ", 3],
    ["", null],
    ["1.5", null],
    ["abc", null],
  ])("wholeNumber_%s_Is%s", (text, expected) => {
    expect(wholeNumber(text)).toBe(expected);
  });
});

describe("payableSchema", () => {
  it("payableSchema_ChequeWithoutNumbers_IsAccepted", () => {
    expect(messages(cheque)).toEqual([]);
  });

  it("payableSchema_PastDate_IsAccepted", () => {
    // §9: one written long ago can be entered late.
    expect(messages({ ...cheque, dueDate: "2020-01-01" })).toEqual([]);
  });

  it("payableSchema_PayeeAtTheLimit_IsAcceptedAndOneMoreIsRefused", () => {
    expect(messages({ ...cheque, payee: "ب".repeat(200) })).toEqual([]);
    expect(messages({ ...cheque, payee: "ب".repeat(201) })).toEqual([
      "نام گیرنده بیش از حد طولانی است.",
    ]);
  });

  it("payableSchema_BlankPayeeDescriptionAndCategory_AreRefused", () => {
    expect(messages({ ...cheque, payee: "  ", description: " ", categoryId: "" })).toEqual([
      "نام گیرنده را وارد کنید.",
      "شرح را وارد کنید.",
      "دسته‌بندی هزینه را انتخاب کنید.",
    ]);
  });

  it("payableSchema_InstallmentInPersianDigits_IsAccepted", () => {
    expect(messages({ ...installment, installmentNumber: "۳", installmentCount: "۱۲" })).toEqual(
      [],
    );
  });

  it("payableSchema_InstallmentWithoutNumbers_IsRefused", () => {
    expect(messages({ ...installment, installmentNumber: "", installmentCount: "" })).toEqual([
      "تعداد کل قسط‌ها را وارد کنید.",
      "شمارهٔ قسط را وارد کنید.",
    ]);
  });

  it("payableSchema_InstallmentNumberAboveTheCount_IsRefused", () => {
    expect(messages({ ...installment, installmentNumber: "13" })).toEqual([
      "شمارهٔ قسط باید بین ۱ و تعداد کل قسط‌ها باشد.",
    ]);
  });

  it("payableSchema_InstallmentCountOverThreeHundredSixty_IsRefused", () => {
    expect(messages({ ...installment, installmentCount: "361" })).toEqual([
      "تعداد کل قسط‌ها باید بین ۱ و ۳۶۰ باشد.",
    ]);
  });

  it("payableSchema_ChequeWithLeftoverNumbers_IsAccepted", () => {
    // Switching the kind back to چک leaves the boxes hidden; the form sends no numbers for a cheque.
    expect(messages({ ...cheque, installmentNumber: "abc" })).toEqual([]);
  });
});

describe("reason schemas", () => {
  it("cancelPayableSchema_BlankReason_IsRefused", () => {
    expect(cancelPayableSchema.safeParse({ reason: "  " }).success).toBe(false);
  });

  it("revertPayableSchema_ReasonAtTheLimit_IsAcceptedAndOneMoreIsRefused", () => {
    expect(revertPayableSchema.safeParse({ reason: "ر".repeat(500) }).success).toBe(true);
    expect(revertPayableSchema.safeParse({ reason: "ر".repeat(501) }).success).toBe(false);
  });
});

describe("canPayToday", () => {
  it.each([
    ["Cheque", "2026-10-04", true],
    ["Cheque", "2026-10-05", true],
    ["Cheque", "2026-10-06", false],
    ["Installment", "2026-12-01", true],
  ] as const)("canPayToday_%s_%s_OnTheFifth_Is%s", (kind, dueDate, expected) => {
    expect(canPayToday(kind, dueDate, "2026-10-05")).toBe(expected);
  });
});
