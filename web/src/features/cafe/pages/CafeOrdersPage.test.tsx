import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { cafePage, cancelledOrder, orderOnAccount, walkInOrder } from "@/test/cafe";
import { json, mockApi, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { reza } from "@/test/members";
import { confirmMoneyReceived, pickMethod } from "@/test/payments";
import { renderApp } from "@/test/renderApp";

const history = {
  ...signedInHandlers(staffUser),
  "GET /api/cafe/orders": () => cafePage([walkInOrder, orderOnAccount, cancelledOrder]),
};

function rowOf(order: { id: string }, text: string) {
  // Rows are found by something only that order shows; the id keeps a failure readable.
  const row = screen.getAllByText(text)[0]!.closest("tr");
  if (row === null) {
    throw new Error(`No row for order ${order.id}`);
  }
  return row;
}

describe("CafeOrdersPage", () => {
  it("Orders_Loaded_ShowsWalkInsMembersAndCancelledOrdersMarked", async () => {
    mockApi(history);
    renderApp("/cafe/orders", { session: session() });

    expect(await screen.findByRole("link", { name: "رضا احمدی" })).toHaveAttribute(
      "href",
      `/members/${reza.id}`,
    );
    expect(screen.getAllByText("مشتری آزاد")).toHaveLength(2);

    const cancelledRow = rowOf(cancelledOrder, "لغو شده");
    expect(within(cancelledRow).getByText(/اشتباه در ثبت/)).toBeInTheDocument();
    // A cancelled order takes no payment and cannot be cancelled twice (§8).
    expect(within(cancelledRow).queryByRole("button")).not.toBeInTheDocument();

    const paidRow = rowOf(walkInOrder, "آب معدنی × ۲");
    expect(within(paidRow).queryByRole("button", { name: /ثبت پرداخت/ })).not.toBeInTheDocument();
    expect(within(paidRow).getByRole("button", { name: /^لغو/ })).toBeInTheDocument();
  });

  it("Orders_DateRangeInTheUrl_IsSentAsFromAndTo", async () => {
    const api = mockApi(history);
    renderApp("/cafe/orders?from=2026-09-01&to=2026-09-30", { session: session() });

    await screen.findByText("لغو شده");
    const url = new URL(api.requestsTo("GET", "/api/cafe/orders")[0]!.url);
    expect(url.searchParams.get("From")).toBe("2026-09-01");
    expect(url.searchParams.get("To")).toBe("2026-09-30");
    // The boxes speak Jalali.
    expect(screen.getByLabelText("از تاریخ")).toHaveValue("۱۴۰۵/۰۶/۱۰");
  });

  it("Orders_BackwardsDateRange_SaysSoWithoutAskingTheApi", async () => {
    const api = mockApi(history);
    renderApp("/cafe/orders?from=2026-09-30&to=2026-09-01", { session: session() });

    expect(await screen.findByText("بازهٔ تاریخ نامعتبر است.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/cafe/orders")).toHaveLength(0);
  });

  it("Orders_CancelPaidOrder_WarnsAboutTheRefundAndRequiresAReason", async () => {
    const api = mockApi({
      ...history,
      [`POST /api/cafe/orders/${walkInOrder.id}/cancel`]: () =>
        json(200, { ...walkInOrder, cancelledAt: "2026-09-26T10:00:00Z", cancelReason: "x" }),
    });
    renderApp("/cafe/orders", { session: session() });

    await screen.findByText("لغو شده");
    fireEvent.click(
      within(rowOf(walkInOrder, "آب معدنی × ۲")).getByRole("button", { name: /^لغو/ }),
    );

    expect(
      screen.getByText(/۵۰٬۰۰۰ تومان پرداخت‌شده برای این سفارش، به همان روشی که پرداخت شده بود/),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "تأیید لغو" }));
    expect(await screen.findByText("دلیل لغو را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/cafe/orders/${walkInOrder.id}/cancel`)).toHaveLength(0);

    fireEvent.change(screen.getByLabelText("دلیل لغو"), { target: { value: "دو بار زده شد" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید لغو" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "سفارش لغو شد و مبلغ پرداخت‌شده بازگردانده شد.",
    );
    const [request] = api.requestsTo("POST", `/api/cafe/orders/${walkInOrder.id}/cancel`);
    expect(await request!.json()).toEqual({ reason: "دو بار زده شد" });
  });

  it("Orders_PayAnOrderOnAccount_OffersWhatIsOwedAndSendsIt", async () => {
    const api = mockApi({
      ...history,
      [`POST /api/cafe/orders/${orderOnAccount.id}/payments`]: () => json(201, { id: "payment" }),
    });
    renderApp("/cafe/orders", { session: session() });

    await screen.findByText("لغو شده");
    fireEvent.click(screen.getByRole("button", { name: /^ثبت پرداخت برای/ }));

    expect(screen.getByLabelText("مبلغ (تومان)")).toHaveValue("۱۲۰٬۰۰۰");
    pickMethod(document.body);
    fireEvent.click(screen.getByRole("button", { name: "تأیید پرداخت" }));
    await confirmMoneyReceived();

    expect(await screen.findByRole("status")).toHaveTextContent("پرداخت ثبت شد.");
    const [request] = api.requestsTo("POST", `/api/cafe/orders/${orderOnAccount.id}/payments`);
    expect(await request!.json()).toEqual({
      amount: "120000",
      method: "Cash",
      referenceNumber: null,
    });
    await waitFor(() => expect(api.requestsTo("GET", "/api/cafe/orders")).toHaveLength(2));
  });

  it("Orders_UnpaidGuestFilter_IsSentAndShowsTheGuestByName", async () => {
    // What the nightly job left on a guest's visit (BUSINESS_RULES.md §7 *Guest visit*).
    const guestOrder = {
      ...orderOnAccount,
      memberId: null,
      memberFullName: null,
      guestName: "مریم احمدی",
      attendanceId: "0199a000-0000-7000-8000-0000000000c9",
    };
    const api = mockApi({
      ...history,
      "GET /api/cafe/orders": () => cafePage([guestOrder]),
    });
    renderApp("/cafe/orders", { session: session() });

    fireEvent.click(await screen.findByLabelText("فقط پرداخت‌نشده — مهمان"));

    await waitFor(() =>
      expect(
        api
          .requestsTo("GET", "/api/cafe/orders")
          .some((request) => new URL(request.url).searchParams.get("UnpaidGuest") === "true"),
      ).toBe(true),
    );
    const row = rowOf(guestOrder, "مریم احمدی");
    expect(row).toHaveTextContent("پرداخت‌نشده — مهمان");
    expect(within(row).queryByRole("link")).not.toBeInTheDocument();
    expect(within(row).getByRole("button", { name: /ثبت پرداخت/ })).toBeInTheDocument();
  });
});
