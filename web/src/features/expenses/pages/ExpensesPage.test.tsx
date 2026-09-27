import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import {
  categoriesPage,
  electricity,
  expensesPage,
  rent,
  septemberRent,
  voidedBill,
} from "@/test/expenses";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { gymToday } from "@/lib/format";
import { renderApp } from "@/test/renderApp";

const books = {
  ...signedInHandlers(owner),
  "GET /api/expenses/categories": () => categoriesPage([rent, electricity]),
  // The total leaves the voided bill out; the API works it out, the page only shows it.
  "GET /api/expenses": () => expensesPage([voidedBill, septemberRent], 50000000),
};

function rowWith(text: string) {
  const row = screen.getAllByText(text)[0]!.closest("tr");
  if (row === null) {
    throw new Error(`No row shows ${text}`);
  }
  return row;
}

/** The form that holds a given field: the new-expense card, or the one opened under a row. */
function formOf(label: string) {
  const form = screen.getByLabelText(label).closest("form");
  if (form === null) {
    throw new Error(`No form holds ${label}`);
  }
  return form;
}

describe("ExpensesPage", () => {
  it("ExpensesPage_Staff_SeesNoAccessMessageAndNoRequest", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/expenses", { session: session() });

    // §1: expenses are the Owner's, reading included.
    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/expenses")).toHaveLength(0);
  });

  it("ExpensesPage_Loaded_ShowsTheTotalAndVoidedRowsMarkedWithoutActions", async () => {
    mockApi(books);
    renderApp("/expenses", { session: session() });

    expect(await screen.findByLabelText("جمع هزینه‌ها")).toHaveTextContent("۵۰٬۰۰۰٬۰۰۰ تومان");
    expect(screen.getByText("هزینه‌های باطل‌شده در این جمع حساب نمی‌شوند.")).toBeInTheDocument();

    const voidedRow = rowWith("قبض برق");
    expect(within(voidedRow).getByText("باطل شده")).toBeInTheDocument();
    expect(within(voidedRow).getByText(/دو بار ثبت شد/)).toBeInTheDocument();
    // A voided expense is final (§9): nothing to edit, nothing to void again.
    expect(within(voidedRow).queryByRole("button")).not.toBeInTheDocument();

    const rentRow = rowWith("اجارهٔ شهریور");
    expect(within(rentRow).getByText("مرجع: TR-4412")).toBeInTheDocument();
    expect(within(rentRow).getByRole("button", { name: /^ویرایش/ })).toBeInTheDocument();
    expect(within(rentRow).getByRole("button", { name: /^ابطال/ })).toBeInTheDocument();
  });

  it("ExpensesPage_FiltersInTheUrl_AreSentAsFromToAndCategory", async () => {
    const api = mockApi(books);
    renderApp(`/expenses?from=2026-09-01&to=2026-09-30&category=${rent.id}`, {
      session: session(),
    });

    await screen.findByText("اجارهٔ شهریور");
    const url = new URL(api.requestsTo("GET", "/api/expenses")[0]!.url);
    expect(url.searchParams.get("From")).toBe("2026-09-01");
    expect(url.searchParams.get("To")).toBe("2026-09-30");
    expect(url.searchParams.get("CategoryId")).toBe(rent.id);
    // The boxes speak Jalali.
    expect(screen.getByLabelText("از تاریخ")).toHaveValue("۱۴۰۵/۰۶/۱۰");
    expect(screen.getByText("جمع هزینه‌ها در این فیلتر")).toBeInTheDocument();
  });

  it("ExpensesPage_ChooseCategory_AsksTheApiForThatCategory", async () => {
    const api = mockApi(books);
    renderApp("/expenses", { session: session() });

    await screen.findByText("اجارهٔ شهریور");
    await screen.findByRole("option", { name: "برق" });
    fireEvent.change(screen.getByLabelText("دسته‌بندی"), { target: { value: electricity.id } });

    await waitFor(() => expect(api.requestsTo("GET", "/api/expenses")).toHaveLength(2));
    const url = new URL(api.requestsTo("GET", "/api/expenses")[1]!.url);
    expect(url.searchParams.get("CategoryId")).toBe(electricity.id);
  });

  it("ExpensesPage_BackwardsDateRange_SaysSoWithoutAskingTheApi", async () => {
    const api = mockApi(books);
    renderApp("/expenses?from=2026-09-30&to=2026-09-01", { session: session() });

    expect(await screen.findByText("بازهٔ تاریخ نامعتبر است.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/expenses")).toHaveLength(0);
  });

  it("ExpensesPage_Record_SendsTheAmountAsDecimalTextAndTodayByDefault", async () => {
    const api = mockApi({
      ...books,
      "POST /api/expenses": () => json(201, { ...septemberRent, id: "new" }),
    });
    renderApp("/expenses", { session: session() });

    await screen.findByText("اجارهٔ شهریور");
    fireEvent.click(screen.getByRole("button", { name: "ثبت هزینه" }));

    const form = formOf("شرح");
    fireEvent.change(within(form).getByLabelText("مبلغ (تومان)"), {
      target: { value: "۱۲۰۰۰۰۰" },
    });
    fireEvent.change(within(form).getByLabelText("دسته‌بندی"), {
      target: { value: electricity.id },
    });
    fireEvent.change(within(form).getByLabelText("شرح"), { target: { value: "  قبض برق مهر " } });
    fireEvent.click(within(form).getByRole("button", { name: "ثبت هزینه" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "هزینه به مبلغ ۱٬۲۰۰٬۰۰۰ تومان ثبت شد.",
    );
    const [request] = api.requestsTo("POST", "/api/expenses");
    expect(await request!.json()).toEqual({
      amount: "1200000",
      categoryId: electricity.id,
      expenseDate: gymToday(),
      description: "قبض برق مهر",
      referenceNumber: null,
    });
  });

  it("ExpensesPage_RecordWithFutureDateAndNoDescription_IsRefusedBeforeSending", async () => {
    const api = mockApi(books);
    renderApp("/expenses", { session: session() });

    await screen.findByText("اجارهٔ شهریور");
    fireEvent.click(screen.getByRole("button", { name: "ثبت هزینه" }));

    const form = formOf("شرح");
    fireEvent.change(within(form).getByLabelText("مبلغ (تومان)"), { target: { value: "0" } });
    fireEvent.change(within(form).getByLabelText("تاریخ هزینه"), {
      target: { value: "۱۵۰۰/۰۱/۰۱" },
    });
    fireEvent.click(within(form).getByRole("button", { name: "ثبت هزینه" }));

    expect(
      await within(form).findByText("تاریخ هزینه نمی‌تواند بعد از امروز باشد."),
    ).toBeInTheDocument();
    expect(within(form).getByText("مبلغ باید بزرگ‌تر از صفر باشد.")).toBeInTheDocument();
    expect(within(form).getByText("دسته‌بندی هزینه را انتخاب کنید.")).toBeInTheDocument();
    expect(within(form).getByText("شرح هزینه را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/expenses")).toHaveLength(0);
  });

  it("ExpensesPage_Edit_SendsTheChangesWithTheVersionItWasReadAt", async () => {
    const api = mockApi({
      ...books,
      [`PUT /api/expenses/${septemberRent.id}`]: () =>
        json(200, { ...septemberRent, amount: 55000000, version: 8 }),
    });
    renderApp("/expenses", { session: session() });

    await screen.findByText("اجارهٔ شهریور");
    fireEvent.click(within(rowWith("اجارهٔ شهریور")).getByRole("button", { name: /^ویرایش/ }));

    const form = formOf("شرح");
    // The form opens on the expense as it stands.
    expect(within(form).getByLabelText("مبلغ (تومان)")).toHaveValue("۵۰٬۰۰۰٬۰۰۰");
    expect(within(form).getByLabelText("تاریخ هزینه")).toHaveValue("۱۴۰۵/۰۶/۱۰");
    fireEvent.change(within(form).getByLabelText("مبلغ (تومان)"), {
      target: { value: "55,000,000" },
    });
    fireEvent.click(within(form).getByRole("button", { name: "ذخیرهٔ تغییرات" }));

    expect(await screen.findByRole("status")).toHaveTextContent("هزینه ویرایش شد.");
    const [request] = api.requestsTo("PUT", `/api/expenses/${septemberRent.id}`);
    expect(await request!.json()).toEqual({
      amount: "55000000",
      categoryId: rent.id,
      expenseDate: "2026-09-01",
      description: "اجارهٔ شهریور",
      referenceNumber: "TR-4412",
      version: 7,
    });
  });

  it("ExpensesPage_EditCrossingAnotherEdit_SaysSoOnTheForm", async () => {
    mockApi({
      ...books,
      [`PUT /api/expenses/${septemberRent.id}`]: () => problem(409, "Expenses.ChangedConcurrently"),
    });
    renderApp("/expenses", { session: session() });

    await screen.findByText("اجارهٔ شهریور");
    fireEvent.click(within(rowWith("اجارهٔ شهریور")).getByRole("button", { name: /^ویرایش/ }));
    const form = formOf("شرح");
    fireEvent.click(within(form).getByRole("button", { name: "ذخیرهٔ تغییرات" }));

    expect(
      await within(form).findByText(/این هزینه هم‌زمان توسط شخص دیگری تغییر کرد/),
    ).toBeInTheDocument();
  });

  it("ExpensesPage_Void_RequiresAReasonAndSendsIt", async () => {
    const api = mockApi({
      ...books,
      [`POST /api/expenses/${septemberRent.id}/void`]: () =>
        json(200, { ...septemberRent, isVoided: true }),
    });
    renderApp("/expenses", { session: session() });

    await screen.findByText("اجارهٔ شهریور");
    fireEvent.click(within(rowWith("اجارهٔ شهریور")).getByRole("button", { name: /^ابطال/ }));

    expect(screen.getByText(/دیگر قابل ویرایش نیست/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "تأیید ابطال" }));
    expect(await screen.findByText("دلیل ابطال را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/expenses/${septemberRent.id}/void`)).toHaveLength(0);

    fireEvent.change(screen.getByLabelText("دلیل ابطال"), { target: { value: "مبلغ اشتباه" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید ابطال" }));

    expect(await screen.findByRole("status")).toHaveTextContent("هزینه باطل شد.");
    const [request] = api.requestsTo("POST", `/api/expenses/${septemberRent.id}/void`);
    expect(await request!.json()).toEqual({ reason: "مبلغ اشتباه" });
  });

  it("ExpensesPage_AddCategoryWithATakenName_SaysSoUnderTheBox", async () => {
    const api = mockApi({
      ...books,
      "POST /api/expenses/categories": () => problem(409, "ExpenseCategories.NameAlreadyExists"),
    });
    renderApp("/expenses", { session: session() });

    await screen.findByRole("list", { name: "فهرست دسته‌بندی‌های هزینه" });
    fireEvent.change(screen.getByLabelText("دسته‌بندی جدید"), { target: { value: "برق" } });
    fireEvent.click(screen.getByRole("button", { name: "افزودن" }));

    expect(
      await screen.findByText("دسته‌بندی هزینهٔ دیگری با همین نام وجود دارد."),
    ).toBeInTheDocument();
    const [request] = api.requestsTo("POST", "/api/expenses/categories");
    expect(await request!.json()).toEqual({ name: "برق" });
  });

  it("ExpensesPage_RenameCategory_SendsTheNewNameWithItsVersion", async () => {
    const api = mockApi({
      ...books,
      [`PUT /api/expenses/categories/${electricity.id}`]: () =>
        json(200, { ...electricity, name: "برق و گاز", version: 3 }),
    });
    renderApp("/expenses", { session: session() });

    const list = await screen.findByRole("list", { name: "فهرست دسته‌بندی‌های هزینه" });
    // No delete and no switch: a category is only added or renamed (§9).
    expect(within(list).queryByRole("button", { name: /حذف/ })).not.toBeInTheDocument();
    fireEvent.click(within(list).getByRole("button", { name: "تغییر نام برق" }));
    fireEvent.change(screen.getByLabelText("نام تازهٔ برق"), { target: { value: "برق و گاز" } });
    fireEvent.click(within(list).getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByRole("status")).toHaveTextContent("نام دسته‌بندی تغییر کرد.");
    const [request] = api.requestsTo("PUT", `/api/expenses/categories/${electricity.id}`);
    expect(await request!.json()).toEqual({ name: "برق و گاز", version: 2 });
  });
});
