import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { cardioCharge, currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { cafePage } from "@/test/cafe";
import { allLockers, heldLocker, lockerId, lockersPage } from "@/test/lockers";
import { json, mockApi, session, signedInHandlers, staffUser, type Handler } from "@/test/mockApi";
import { memberDebt, reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";
import { subscriptionsPage } from "@/test/subscriptions";
import type { Attendance } from "@/features/attendance/api";
import type { ServiceCharge } from "@/features/serviceCharges/api";

/**
 * «فروشگاه» and «آنالیز» in a visit's locker box (BUSINESS_RULES.md §7 *Sale at the desk*, tasks
 * 6.5.28 and 6.5.29), each on a coloured tile of its own beside هوازی and بوفه. فروشگاه takes one
 * or more named items, with ▲/▼ for the quantity; آنالیز takes only a price. Neither takes money
 * when it is recorded: both go on the member's account.
 */
describe("SaleBox", () => {
  const shopPath = (visit: Attendance) => `/api/attendance/${visit.id}/service-charges/shop`;
  const chargePath = (visit: Attendance) => `/api/attendance/${visit.id}/service-charges`;

  /** Reza inside on locker 2. */
  function onLocker(visit: Attendance): Attendance {
    return { ...visit, lockerId: lockerId(2), lockerNumber: 2 };
  }

  function shopItem(visit: Attendance, overrides: Partial<ServiceCharge> = {}): ServiceCharge {
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

  function analysis(visit: Attendance, overrides: Partial<ServiceCharge> = {}): ServiceCharge {
    return {
      ...cardioCharge(visit),
      id: "0199a000-0000-7000-8000-0000000000f8",
      kind: "Analysis",
      amount: 200000,
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

  /** Opens the shop form from its tile. */
  async function openShop() {
    fireEvent.click(await screen.findByRole("button", { name: "فروشگاه" }));

    return screen.findByRole("dialog", { name: /^فروشگاه/ });
  }

  /** Types one item into the line named `line` («کالای ۱», «کالای ۲», …). */
  function fillLine(form: HTMLElement, line: string, name: string, price: string) {
    const fields = within(form).getByRole("group", { name: line });
    fireEvent.change(within(fields).getByLabelText("نام کالا"), { target: { value: name } });
    fireEvent.change(within(fields).getByLabelText("قیمت واحد (تومان)"), {
      target: { value: price },
    });

    return fields;
  }

  /** Task 6.5.29: four tiles that name themselves, with no heading above them. */
  it("Box_OpenVisit_OffersFourTilesWithoutHeadings", async () => {
    await renderLocker(onLocker(openVisit(reza.id)));

    const box = await screen.findByRole("dialog");
    for (const tile of ["هوازی", "بوفه", "فروشگاه", "آنالیز"]) {
      expect(within(box).getByRole("button", { name: tile })).toBeInTheDocument();
    }
    expect(within(box).queryByText("متفرقه")).not.toBeInTheDocument();
    expect(within(box).queryByRole("button", { name: "مبلغ هوازی" })).not.toBeInTheDocument();
  });

  /** Decided 1405/07/12: no payment is asked for; the sale goes on the member's account. */
  it("Shop_OneItem_PostsItWithNoPaymentAndNoMoneyQuestion", async () => {
    const visit = onLocker(openVisit(reza.id));
    const api = await renderLocker(visit, {
      [`POST ${shopPath(visit)}`]: () => json(200, [shopItem(visit)]),
    });

    const form = await openShop();
    expect(within(form).queryByLabelText("نوع پرداخت")).not.toBeInTheDocument();
    const line = fillLine(form, "کالای ۱", "دستکش", "150000");
    fireEvent.change(within(line).getByLabelText("تعداد"), { target: { value: "۲" } });
    expect(within(form).getByText("۳۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    fireEvent.click(within(form).getByRole("button", { name: "ثبت فروش" }));

    await waitFor(() => expect(api.requestsTo("POST", shopPath(visit))).toHaveLength(1));
    expect(await api.requestsTo("POST", shopPath(visit))[0]!.json()).toEqual({
      items: [{ description: "دستکش", quantity: 2, unitPrice: "150000" }],
    });
    expect(screen.queryByRole("dialog", { name: "آیا پول دریافت شد؟" })).not.toBeInTheDocument();
    expect(await screen.findByText("فروش فروشگاه ثبت شد")).toBeInTheDocument();
    expect(screen.getByText(/به حساب رضا احمدی/)).toBeInTheDocument();
  });

  /** ▲ one more, ▼ one fewer, never below one. */
  it("Shop_StepperButtons_ChangeTheQuantityAndStopAtOne", async () => {
    const visit = onLocker(openVisit(reza.id));
    await renderLocker(visit);

    const form = await openShop();
    const quantity = within(form).getByLabelText("تعداد");
    expect(quantity).toHaveValue("۱");

    fireEvent.click(within(form).getByRole("button", { name: "یکی بیشتر" }));
    fireEvent.click(within(form).getByRole("button", { name: "یکی بیشتر" }));
    expect(quantity).toHaveValue("۳");

    fireEvent.click(within(form).getByRole("button", { name: "یکی کمتر" }));
    fireEvent.click(within(form).getByRole("button", { name: "یکی کمتر" }));
    fireEvent.click(within(form).getByRole("button", { name: "یکی کمتر" }));
    expect(quantity).toHaveValue("۱");
  });

  /** Two things sold together: one more line, one request, both on the total. */
  it("Shop_SecondItemAdded_PostsBothInOneRequest", async () => {
    const visit = onLocker(openVisit(reza.id));
    const api = await renderLocker(visit, {
      [`POST ${shopPath(visit)}`]: () =>
        json(200, [
          shopItem(visit, { quantity: 1, amount: 150000 }),
          shopItem(visit, {
            id: "0199a000-0000-7000-8000-0000000000f9",
            description: "حوله",
            quantity: 1,
            unitPrice: 80000,
            amount: 80000,
          }),
        ]),
    });

    const form = await openShop();
    fillLine(form, "کالای ۱", "دستکش", "150000");
    fireEvent.click(within(form).getByRole("button", { name: "افزودن کالای دیگر" }));
    fillLine(form, "کالای ۲", "حوله", "80000");
    expect(within(form).getByText("۲۳۰٬۰۰۰ تومان")).toBeInTheDocument();
    fireEvent.click(within(form).getByRole("button", { name: "ثبت فروش" }));

    await waitFor(() => expect(api.requestsTo("POST", shopPath(visit))).toHaveLength(1));
    expect(await api.requestsTo("POST", shopPath(visit))[0]!.json()).toEqual({
      items: [
        { description: "دستکش", quantity: 1, unitPrice: "150000" },
        { description: "حوله", quantity: 1, unitPrice: "80000" },
      ],
    });
  });

  it("Shop_SecondItemRemoved_LeavesOneLine", async () => {
    await renderLocker(onLocker(openVisit(reza.id)));

    const form = await openShop();
    fireEvent.click(within(form).getByRole("button", { name: "افزودن کالای دیگر" }));
    expect(within(form).getByRole("group", { name: "کالای ۲" })).toBeInTheDocument();

    fireEvent.click(within(form).getByRole("button", { name: "حذف کالای ۲" }));

    expect(within(form).queryByRole("group", { name: "کالای ۲" })).not.toBeInTheDocument();
    expect(within(form).queryByRole("button", { name: /^حذف کالای/ })).not.toBeInTheDocument();
  });

  it("Shop_NameMissing_IsRefusedBeforeAnythingIsSent", async () => {
    const visit = onLocker(openVisit(reza.id));
    const api = await renderLocker(visit);

    const form = await openShop();
    fillLine(form, "کالای ۱", "", "150000");
    fireEvent.click(within(form).getByRole("button", { name: "ثبت فروش" }));
    expect(await within(form).findByText("نام کالا را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", shopPath(visit))).toHaveLength(0);
  });

  /** Task 6.5.29: آنالیز is only a price, recorded like هوازی and put on the account. */
  it("Analysis_OnlyAPrice_PostsItUnderAnalysis", async () => {
    const visit = onLocker(openVisit(reza.id));
    const api = await renderLocker(visit, {
      [`POST ${chargePath(visit)}`]: () => json(201, analysis(visit)),
    });

    fireEvent.click(await screen.findByRole("button", { name: "آنالیز" }));
    const form = await screen.findByRole("dialog", { name: /^آنالیز/ });
    expect(within(form).queryByLabelText("نام کالا")).not.toBeInTheDocument();
    expect(within(form).queryByLabelText("تعداد")).not.toBeInTheDocument();
    fireEvent.change(within(form).getByLabelText("مبلغ آنالیز"), { target: { value: "200000" } });
    fireEvent.click(within(form).getByRole("button", { name: "ثبت" }));

    await waitFor(() => expect(api.requestsTo("POST", chargePath(visit))).toHaveLength(1));
    expect(await api.requestsTo("POST", chargePath(visit))[0]!.json()).toEqual({
      kind: "Analysis",
      amount: "200000",
    });
    expect(await screen.findByText("آنالیز ثبت شد")).toBeInTheDocument();
  });

  it("Box_WithAShopItem_ShowsItsTotalAndListsItWithItsQuantity", async () => {
    const visit = onLocker(openVisit(reza.id));
    await renderLocker({ ...visit, serviceCharges: [shopItem(visit)] });

    fireEvent.click(await screen.findByRole("button", { name: /^فروشگاه:/ }));

    const list = await screen.findByRole("list", { name: "فروش‌های فروشگاه" });
    expect(within(list).getByText(/دستکش × ۲/)).toBeInTheDocument();
    expect(within(list).getByRole("button", { name: "ثبت پرداخت" })).toBeInTheDocument();
    expect(within(list).getByRole("button", { name: "ابطال" })).toBeInTheDocument();
  });

  /** Each tile totals only its own kind: an آنالیز does not show on the فروشگاه tile. */
  it("Box_WithAnAnalysis_ShowsItOnTheAnalysisTileOnly", async () => {
    const visit = onLocker(openVisit(reza.id));
    await renderLocker({ ...visit, serviceCharges: [analysis(visit)] });

    expect(
      await screen.findByRole("button", { name: "آنالیز: ۲۰۰٬۰۰۰ تومان" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "فروشگاه" })).toBeInTheDocument();
  });

  /** §7 *Cancel check-in*: each sale has its own tick, sent by id, whichever tile it came from. */
  it("CancelCheckIn_SaleOfEachKindTicked_SendsBothIds", async () => {
    const visit = onLocker(openVisit(reza.id));
    const shop = shopItem(visit);
    const scan = analysis(visit);
    const cancelPath = `/api/attendance/${visit.id}/cancel`;
    const api = await renderLocker(
      { ...visit, serviceCharges: [shop, scan] },
      {
        [`POST ${cancelPath}`]: () => json(200, { ...visit, checkedOutAt: "2026-09-18T07:10:00Z" }),
      },
    );

    fireEvent.click(await screen.findByRole("button", { name: "لغو ورود" }));
    fireEvent.click(await screen.findByRole("checkbox", { name: /فروشگاه: دستکش × ۲/ }));
    fireEvent.click(screen.getByRole("checkbox", { name: /^آنالیز/ }));
    fireEvent.click(screen.getByRole("button", { name: "بله، ورود لغو شود" }));
    fireEvent.click(await screen.findByRole("button", { name: "بله، ورود و این موارد لغو شوند" }));

    await waitFor(() => expect(api.requestsTo("POST", cancelPath)).toHaveLength(1));
    expect(await api.requestsTo("POST", cancelPath)[0]!.json()).toEqual({
      voidCardio: false,
      cafeOrderIds: [],
      saleIds: [shop.id, scan.id],
    });
  });
});
