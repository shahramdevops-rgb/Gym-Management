import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { cafePage, chips, drinks, snacks, water } from "@/test/cafe";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

const menu = {
  // Staff, not the Owner: the whole cafe is front-desk work (BUSINESS_RULES.md §8).
  ...signedInHandlers(staffUser),
  "GET /api/cafe/categories": () => cafePage([drinks, snacks]),
  "GET /api/cafe/products": () => cafePage([water, chips]),
};

function categoryRow(name: string) {
  const list = screen.getByRole("list", { name: "فهرست دسته‌بندی‌ها" });
  return within(list).getByText(name).closest("li")!;
}

describe("CafeMenuPage", () => {
  it("Menu_StaffUser_SeesCategoriesAndProductsWithTheirStates", async () => {
    const api = mockApi(menu);
    renderApp("/cafe/menu", { session: session() });

    const chipsRow = (await screen.findByText("چیپس")).closest("tr")!;
    // Switched on, but its shelf is off: the screen says why the till will not offer it.
    expect(within(chipsRow).getByText("موجود")).toBeInTheDocument();
    expect(within(chipsRow).getByText(/دسته‌بندی ناموجود است/)).toBeInTheDocument();

    expect(within(categoryRow("تنقلات")).getByText("ناموجود")).toBeInTheDocument();
    // The management list asks for everything, not only what is sellable.
    const url = new URL(api.requestsTo("GET", "/api/cafe/products")[0]!.url);
    expect(url.searchParams.has("IsActive")).toBe(false);
  });

  it("Menu_AddCategoryWithATakenName_ShowsTheErrorUnderTheField", async () => {
    mockApi({
      ...menu,
      "POST /api/cafe/categories": () => problem(409, "ProductCategories.NameAlreadyExists"),
    });
    renderApp("/cafe/menu", { session: session() });

    await screen.findByText("چیپس");
    fireEvent.change(screen.getByLabelText("دسته‌بندی جدید"), { target: { value: "نوشیدنی" } });
    fireEvent.click(screen.getByRole("button", { name: "افزودن" }));

    expect(await screen.findByText("دسته‌بندی دیگری با همین نام وجود دارد.")).toBeInTheDocument();
  });

  it("Menu_SwitchCategoryOff_CallsDeactivate", async () => {
    const api = mockApi({
      ...menu,
      [`POST /api/cafe/categories/${drinks.id}/deactivate`]: () =>
        json(200, { ...drinks, isActive: false }),
    });
    renderApp("/cafe/menu", { session: session() });

    await screen.findByText("چیپس");
    fireEvent.click(screen.getByRole("button", { name: "ناموجود کردن نوشیدنی" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "«نوشیدنی» و همهٔ محصولاتش از بوفه برداشته شد.",
    );
    expect(api.requestsTo("POST", `/api/cafe/categories/${drinks.id}/deactivate`)).toHaveLength(1);
  });

  it("Menu_DeleteCategoryThatHasProducts_ShowsWhyNot", async () => {
    mockApi({
      ...menu,
      [`DELETE /api/cafe/categories/${drinks.id}`]: () =>
        problem(409, "ProductCategories.NotEmpty"),
    });
    renderApp("/cafe/menu", { session: session() });

    await screen.findByText("چیپس");
    fireEvent.click(screen.getByRole("button", { name: "حذف نوشیدنی" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "این دسته‌بندی محصول دارد و حذف نمی‌شود.",
    );
  });

  it("Menu_AddProduct_SendsThePriceAsAPlainDecimal", async () => {
    const api = mockApi({
      ...menu,
      "POST /api/cafe/products": () => json(201, { ...water, id: "new", name: "دوغ" }),
    });
    renderApp("/cafe/menu", { session: session() });

    await screen.findByText("چیپس");
    fireEvent.click(screen.getByRole("button", { name: "محصول جدید" }));
    fireEvent.change(screen.getByLabelText("نام محصول"), { target: { value: "دوغ" } });
    fireEvent.change(screen.getByLabelText("قیمت (تومان)"), { target: { value: "۳۰۰۰۰" } });

    // Grouped and written out in words as it is typed, like every amount in the app.
    expect(screen.getByLabelText("قیمت (تومان)")).toHaveValue("۳۰٬۰۰۰");
    fireEvent.click(screen.getByRole("button", { name: "افزودن محصول" }));

    expect(await screen.findByRole("status")).toHaveTextContent("«دوغ» به منو اضافه شد.");
    const [request] = api.requestsTo("POST", "/api/cafe/products");
    expect(await request!.json()).toEqual({ name: "دوغ", categoryId: drinks.id, price: "30000" });
  });

  it("Menu_AddProductWithoutAPrice_IsRefusedBeforeSending", async () => {
    const api = mockApi(menu);
    renderApp("/cafe/menu", { session: session() });

    await screen.findByText("چیپس");
    fireEvent.click(screen.getByRole("button", { name: "محصول جدید" }));
    fireEvent.change(screen.getByLabelText("نام محصول"), { target: { value: "دوغ" } });
    fireEvent.click(screen.getByRole("button", { name: "افزودن محصول" }));

    expect(await screen.findByText("قیمت را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/cafe/products")).toHaveLength(0);
  });

  it("Menu_EditProduct_SendsTheVersionItWasReadWith", async () => {
    const api = mockApi({
      ...menu,
      [`PUT /api/cafe/products/${water.id}`]: () => json(200, { ...water, price: 30000 }),
    });
    renderApp("/cafe/menu", { session: session() });

    await screen.findByText("چیپس");
    fireEvent.click(screen.getByRole("button", { name: "ویرایش آب معدنی" }));
    fireEvent.change(screen.getByLabelText("قیمت (تومان)"), { target: { value: "30000" } });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    await screen.findByRole("status");
    const [request] = api.requestsTo("PUT", `/api/cafe/products/${water.id}`);
    expect(await request!.json()).toEqual({
      name: "آب معدنی",
      categoryId: drinks.id,
      price: "30000",
      version: water.version,
    });
  });

  it("Menu_EditProductSomebodyElseChanged_ShowsTheConflict", async () => {
    mockApi({
      ...menu,
      [`PUT /api/cafe/products/${water.id}`]: () => problem(409, "Products.ChangedConcurrently"),
    });
    renderApp("/cafe/menu", { session: session() });

    await screen.findByText("چیپس");
    fireEvent.click(screen.getByRole("button", { name: "ویرایش آب معدنی" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "این محصول هم‌زمان توسط شخص دیگری تغییر کرد.",
    );
  });

  it("Menu_SwitchProductOff_CallsDeactivate", async () => {
    const api = mockApi({
      ...menu,
      [`POST /api/cafe/products/${water.id}/deactivate`]: () =>
        json(200, { ...water, isActive: false }),
    });
    renderApp("/cafe/menu", { session: session() });

    await screen.findByText("چیپس");
    fireEvent.click(screen.getByRole("button", { name: "ناموجود کردن آب معدنی" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/cafe/products/${water.id}/deactivate`)).toHaveLength(1),
    );
  });
});
