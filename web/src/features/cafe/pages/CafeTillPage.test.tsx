import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { cafePage, orderOnAccount, proteinShake, walkInOrder, water } from "@/test/cafe";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { membersPage, reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

const menu = {
  ...signedInHandlers(staffUser),
  "GET /api/cafe/products": () => cafePage([water, proteinShake]),
};

/** The product's button in the grid, not the cart's buttons that also carry its name. */
function productButton(name: string) {
  return within(screen.getByRole("region", { name: "نوشیدنی" })).getByRole("button", {
    name: new RegExp(name),
  });
}

describe("CafeTillPage", () => {
  it("Till_Loaded_AsksOnlyForSellableProducts", async () => {
    const api = mockApi(menu);

    renderApp("/cafe", { session: session() });

    expect(await screen.findByRole("region", { name: "نوشیدنی" })).toBeInTheDocument();
    const url = new URL(api.requestsTo("GET", "/api/cafe/products")[0]!.url);
    // BUSINESS_RULES.md §8: the till never offers a ناموجود item.
    expect(url.searchParams.get("IsActive")).toBe("true");
  });

  it("Till_SameProductTappedTwice_RaisesTheQuantityInsteadOfAddingALine", async () => {
    mockApi(menu);
    renderApp("/cafe", { session: session() });

    await screen.findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(productButton("آب معدنی"));
    fireEvent.click(productButton("آب معدنی"));

    const cart = screen.getByRole("list", { name: "سبد خرید" });
    expect(within(cart).getAllByRole("listitem")).toHaveLength(1);
    expect(screen.getByLabelText("تعداد آب معدنی")).toHaveValue("۲");
    expect(within(cart).getByText("۵۰٬۰۰۰ تومان")).toBeInTheDocument();
  });

  it("Till_QuantityTypedWithPersianDigits_UpdatesTheTotal", async () => {
    mockApi(menu);
    renderApp("/cafe", { session: session() });

    await screen.findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(productButton("آب معدنی"));
    fireEvent.change(screen.getByLabelText("تعداد آب معدنی"), { target: { value: "۴" } });

    const total = screen.getByText("جمع کل").parentElement!;
    expect(total).toHaveTextContent("۱۰۰٬۰۰۰ تومان");
  });

  it("Till_WalkInSale_SendsTheWholeTotalAsPaymentAndClearsTheCart", async () => {
    const api = mockApi({
      ...menu,
      "POST /api/cafe/orders": () => json(201, walkInOrder),
    });
    renderApp("/cafe", { session: session() });

    await screen.findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(productButton("آب معدنی"));
    fireEvent.click(productButton("آب معدنی"));

    // A walk-in customer pays everything now (§8), so the amount is the total and locked.
    expect(screen.getByLabelText("مبلغ دریافتی (تومان)")).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "ثبت سفارش" }));

    expect(await screen.findByRole("status")).toHaveTextContent("سفارش ۵۰٬۰۰۰ تومان ثبت شد.");
    const [request] = api.requestsTo("POST", "/api/cafe/orders");
    expect(await request!.json()).toEqual({
      memberId: null,
      items: [{ productId: water.id, quantity: 2 }],
      payment: { amount: "50000.00", method: "Cash", referenceNumber: null },
    });
    expect(screen.getByText("سبد خالی است. روی محصول‌ها بزنید.")).toBeInTheDocument();
  });

  it("Till_MemberWithTheAmountCleared_PutsTheWholeOrderOnTheAccount", async () => {
    const api = mockApi({
      ...menu,
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      "POST /api/cafe/orders": () => json(201, orderOnAccount),
    });
    renderApp(`/cafe?member=${reza.id}`, { session: session() });

    expect(await screen.findByText("رضا احمدی")).toBeInTheDocument();
    await screen.findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(productButton("شیک پروتئین"));
    fireEvent.change(screen.getByLabelText("مبلغ دریافتی (تومان)"), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: "ثبت سفارش" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "۱۲۰٬۰۰۰ تومان به حساب رضا احمدی رفت.",
    );
    const [request] = api.requestsTo("POST", "/api/cafe/orders");
    expect(await request!.json()).toEqual({
      memberId: reza.id,
      items: [{ productId: proteinShake.id, quantity: 1 }],
      payment: null,
    });
  });

  it("Till_MemberPaysPart_SendsThatAmountAndSaysWhatStaysOnTheAccount", async () => {
    const api = mockApi({
      ...menu,
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      "POST /api/cafe/orders": () =>
        json(201, { ...orderOnAccount, netPaid: 50000, outstanding: 70000 }),
    });
    renderApp(`/cafe?member=${reza.id}`, { session: session() });

    await screen.findByText("رضا احمدی");
    await screen.findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(productButton("شیک پروتئین"));
    fireEvent.change(screen.getByLabelText("مبلغ دریافتی (تومان)"), {
      target: { value: "۵۰۰۰۰" },
    });

    expect(screen.getByText("۷۰٬۰۰۰ تومان به حساب رضا احمدی می‌رود.")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("روش پرداخت"), { target: { value: "Card" } });
    fireEvent.click(screen.getByRole("button", { name: "ثبت سفارش" }));

    await screen.findByRole("status");
    const [request] = api.requestsTo("POST", "/api/cafe/orders");
    expect((await request!.json()).payment).toEqual({
      amount: "50000",
      method: "Card",
      referenceNumber: null,
    });
  });

  it("Till_MemberPaysMoreThanTheTotal_IsRefusedBeforeSending", async () => {
    const api = mockApi({
      ...menu,
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
    });
    renderApp(`/cafe?member=${reza.id}`, { session: session() });

    await screen.findByText("رضا احمدی");
    await screen.findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(productButton("آب معدنی"));
    fireEvent.change(screen.getByLabelText("مبلغ دریافتی (تومان)"), {
      target: { value: "30000" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ثبت سفارش" }));

    expect(await screen.findByText("مبلغ پرداختی از مبلغ سفارش بیشتر است.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/cafe/orders")).toHaveLength(0);
  });

  it("Till_ProductSwitchedOffMeanwhile_ShowsThePersianErrorAndKeepsTheCart", async () => {
    const api = mockApi({
      ...menu,
      "POST /api/cafe/orders": () => problem(422, "Products.Inactive"),
    });
    renderApp("/cafe", { session: session() });

    await screen.findByRole("region", { name: "نوشیدنی" });
    fireEvent.click(productButton("آب معدنی"));
    fireEvent.click(screen.getByRole("button", { name: "ثبت سفارش" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "این محصول غیرفعال است و قابل فروش نیست.",
    );
    expect(screen.getByLabelText("تعداد آب معدنی")).toBeInTheDocument();
    // The menu is asked for again, so a product switched off since it loaded drops out.
    await waitFor(() => expect(api.requestsTo("GET", "/api/cafe/products")).toHaveLength(2));
  });

  it("Till_MemberFoundBySearch_BecomesTheCustomer", async () => {
    mockApi({
      ...menu,
      "GET /api/members": () => membersPage([reza]),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
    });
    const { router } = renderApp("/cafe", { session: session() });

    fireEvent.change(await screen.findByLabelText("جستجوی عضو"), { target: { value: "رضا" } });
    fireEvent.click(await screen.findByRole("button", { name: /رضا احمدی/ }));

    await waitFor(() => expect(router.state.location.search).toBe(`?member=${reza.id}`));
    expect(await screen.findByRole("button", { name: "مشتری آزاد" })).toBeInTheDocument();
  });
});
