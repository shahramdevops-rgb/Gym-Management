import { productPriceProblem } from "./schemas";

describe("productPriceProblem", () => {
  it.each(["25000", "۲۵٬۰۰۰", "0", "12.50"])("productPriceProblem_%j_IsValid", (text) => {
    expect(productPriceProblem(text)).toBeNull();
  });

  it.each([
    ["", "قیمت را وارد کنید."],
    ["-1", "قیمت نمی‌تواند منفی باشد."],
    ["12.505", "قیمت حداکثر می‌تواند ۲ رقم اعشار داشته باشد."],
    ["12345678901234567", "قیمت بیش از حد بزرگ است."],
    ["بیست", "قیمت باید یک عدد باشد."],
  ])("productPriceProblem_%j_Returns%j", (text, expected) => {
    expect(productPriceProblem(text)).toBe(expected);
  });
});
