import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { cardioCharge, currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { cafePage } from "@/test/cafe";
import { allLockers, heldLocker, lockerId, lockersPage } from "@/test/lockers";
import { json, mockApi, session, signedInHandlers, staffUser, type Handler } from "@/test/mockApi";
import { memberDebt, reza } from "@/test/members";
import { confirmMoneyReceived, pickMethod } from "@/test/payments";
import { renderApp } from "@/test/renderApp";
import { subscriptionsPage } from "@/test/subscriptions";
import type { Attendance } from "@/features/attendance/api";
import type { ServiceCharge } from "@/features/serviceCharges/api";

/**
 * «متفرقه» in a visit's locker box (BUSINESS_RULES.md §7 *Miscellaneous sale*, task 6.5.28): a
 * sale the desk names itself, with its quantity, price and how it was paid, beside هوازی and بوفه.
 */
describe("MiscellaneousSaleBox", () => {
  /** Reza inside on locker 2. */
  function onLocker(visit: Attendance): Attendance {
    return { ...visit, lockerId: lockerId(2), lockerNumber: 2 };
  }

  function miscSale(visit: Attendance, overrides: Partial<ServiceCharge> = {}): ServiceCharge {
    return {
      ...cardioCharge(visit),
      id: "0199a000-0000-7000-8000-0000000000f7",
      kind: "Miscellaneous",
      description: "دستکش",
      quantity: 2,
      unitPrice: 150000,
      amount: 300000,
      canChangeAmount: false,
      ...overrides,
    };
  }

  /** Opens locker 2's box on the map. */
  async function renderLocker(visit: Attendance, extra: Record<string, Handler> = {}) {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "GET /api/lockers": () => lockersPage(allLockers(heldLocker(2, reza.id, reza.fullName))),
      "GET /api/attendance/currently-inside": () =>
        currentlyInsidePage([insideRow(reza.fullName, visit)]),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt(),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([]),
      "GET /api/cafe/orders": () => cafePage([]),
      ...extra,
    });
    renderApp("/", { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: /^کمد ۲،/ }));
    await screen.findByRole("dialog");

    return api;
  }

  /** Types a sale into the form the «فروش متفرقه» button opens. */
  async function fillSale(payment: string) {
    fireEvent.click(await screen.findByRole("button", { name: "فروش متفرقه" }));
    const form = await screen.findByRole("dialog", { name: /^متفرقه/ });
    fireEvent.change(within(form).getByLabelText("نام کالا"), { target: { value: "دستکش" } });
    fireEvent.change(within(form).getByLabelText("تعداد"), { target: { value: "۲" } });
    fireEvent.change(within(form).getByLabelText("قیمت واحد (تومان)"), {
      target: { value: "150000" },
    });
    if (payment !== "") {
      pickMethod(form, payment as "Card", "نوع پرداخت");
    }

    return form;
  }

  it("Box_OpenVisit_OffersAMiscellaneousSaleBesideCardioAndCafe", async () => {
    await renderLocker(onLocker(openVisit(reza.id)));

    expect(await screen.findByRole("button", { name: "فروش متفرقه" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "مبلغ هوازی" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "خرید بوفه" })).toBeInTheDocument();
  });

  /** §5 *Confirming money at the desk*: money taken now is confirmed before it is written. */
  it("Sell_PaidByCard_AsksIfTheMoneyArrivedThenPostsTheSale", async () => {
    const visit = onLocker(openVisit(reza.id));
    const path = `/api/attendance/${visit.id}/service-charges/miscellaneous`;
    const api = await renderLocker(visit, {
      [`POST ${path}`]: () =>
        json(201, miscSale(visit, { netPaid: 300000, paymentStatus: "Paid" })),
    });

    const form = await fillSale("Card");
    expect(within(form).getByText("۳۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    fireEvent.click(within(form).getByRole("button", { name: "ثبت فروش" }));

    await confirmMoneyReceived();

    await waitFor(() => expect(api.requestsTo("POST", path)).toHaveLength(1));
    expect(await api.requestsTo("POST", path)[0]!.json()).toEqual({
      description: "دستکش",
      quantity: 2,
      unitPrice: "150000",
      method: "Card",
    });
    expect(await screen.findByText("فروش متفرقه ثبت شد")).toBeInTheDocument();
  });

  it("Sell_OnAccount_PostsWithoutAskingAboutMoney", async () => {
    const visit = onLocker(openVisit(reza.id));
    const path = `/api/attendance/${visit.id}/service-charges/miscellaneous`;
    const api = await renderLocker(visit, { [`POST ${path}`]: () => json(201, miscSale(visit)) });

    const form = await fillSale("OnAccount");
    fireEvent.click(within(form).getByRole("button", { name: "ثبت فروش" }));

    await waitFor(() => expect(api.requestsTo("POST", path)).toHaveLength(1));
    expect((await api.requestsTo("POST", path)[0]!.json()).method).toBeNull();
    expect(screen.queryByRole("dialog", { name: "آیا پول دریافت شد؟" })).not.toBeInTheDocument();
  });

  /** No payment is chosen in advance, «به حساب عضو» included (§5). */
  it("Sell_NoPaymentChosen_IsRefusedBeforeAnythingIsSent", async () => {
    const visit = onLocker(openVisit(reza.id));
    const path = `/api/attendance/${visit.id}/service-charges/miscellaneous`;
    const api = await renderLocker(visit);

    const form = await fillSale("");
    fireEvent.click(within(form).getByRole("button", { name: "ثبت فروش" }));

    expect(await within(form).findByText("نوع پرداخت را انتخاب کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", path)).toHaveLength(0);
  });

  it("Box_WithASale_ShowsItsTotalAndListsItWithItsQuantity", async () => {
    const visit = onLocker(openVisit(reza.id));
    await renderLocker({ ...visit, serviceCharges: [miscSale(visit)] });

    fireEvent.click(await screen.findByRole("button", { name: /^متفرقه:/ }));

    const list = await screen.findByRole("list", { name: "فروش‌های متفرقه" });
    expect(within(list).getByText(/دستکش × ۲/)).toBeInTheDocument();
    expect(within(list).getByRole("button", { name: "ثبت پرداخت" })).toBeInTheDocument();
    expect(within(list).getByRole("button", { name: "ابطال" })).toBeInTheDocument();
  });

  /** §7 *Cancel check-in*: each sale has its own tick, sent by id. */
  it("CancelCheckIn_SaleTicked_SendsItsId", async () => {
    const visit = onLocker(openVisit(reza.id));
    const sale = miscSale(visit);
    const cancelPath = `/api/attendance/${visit.id}/cancel`;
    const api = await renderLocker(
      { ...visit, serviceCharges: [sale] },
      { [`POST ${cancelPath}`]: () => json(200, { ...visit, checkedOutAt: "2026-09-18T07:10:00Z" }) },
    );

    fireEvent.click(await screen.findByRole("button", { name: "لغو ورود" }));
    fireEvent.click(await screen.findByRole("checkbox", { name: /متفرقه: دستکش × ۲/ }));
    fireEvent.click(screen.getByRole("button", { name: "بله، ورود لغو شود" }));
    fireEvent.click(await screen.findByRole("button", { name: "بله، ورود و این موارد لغو شوند" }));

    await waitFor(() => expect(api.requestsTo("POST", cancelPath)).toHaveLength(1));
    expect(await api.requestsTo("POST", cancelPath)[0]!.json()).toEqual({
      voidCardio: false,
      cafeOrderIds: [],
      miscellaneousSaleIds: [sale.id],
    });
  });
});
