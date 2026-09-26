import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { Settlement } from "@/features/payments/settle";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { cafeDebtItem, debtItem, memberDebt, reza, serviceChargeDebtItem } from "@/test/members";
import { renderApp } from "@/test/renderApp";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";

/**
 * «تسویه یکجا» on the member profile's debt card (BUSINESS_RULES.md §5 *Settling several items at
 * once*, roadmap 7.5). The member owes the walk-in's three things: a drink, هوازی and the plan.
 */
describe("SettleDebt", () => {
  const settlePath = `/api/members/${reza.id}/settlements`;
  const owed = [debtItem(), serviceChargeDebtItem(), cafeDebtItem()];

  const settled: Settlement = {
    amount: 640000,
    method: "Card",
    payments: [
      {
        paymentId: "p1",
        kind: "CafeOrder",
        targetId: cafeDebtItem().id,
        amount: 30000,
        outstanding: 0,
      },
      {
        paymentId: "p2",
        kind: "ServiceCharge",
        targetId: serviceChargeDebtItem().id,
        amount: 10000,
        outstanding: 0,
      },
      {
        paymentId: "p3",
        kind: "Subscription",
        targetId: debtItem().id,
        amount: 600000,
        outstanding: 0,
      },
    ],
    remainingDebt: 0,
  };

  function profile(
    handlers: Record<string, (request: Request) => Response | Promise<Response>> = {},
  ) {
    return mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt(owed),
      ...handlers,
    });
  }

  async function openForm() {
    renderApp(`/members/${reza.id}`, { session: session() });
    fireEvent.click(await screen.findByRole("button", { name: "تسویه یکجا" }));
    return screen.findByRole("form", { name: "تسویه یکجا" });
  }

  function amountBox(form: HTMLElement): HTMLInputElement {
    return within(form).getByLabelText("مبلغ دریافتی (تومان)");
  }

  it("SettleDebt_Opened_TicksEveryItemInPaymentOrderAndFillsTheTotal", async () => {
    profile();
    const form = await openForm();

    // Cafe, then هوازی, then the plan: the order the money is spent in.
    const boxes = within(form).getAllByRole("checkbox");
    expect(boxes.map((box) => box.closest("label")!.textContent)).toEqual([
      expect.stringContaining("بوفه"),
      expect.stringContaining("هوازی"),
      expect.stringContaining("اشتراک ماهانه"),
    ]);
    boxes.forEach((box) => expect(box).toBeChecked());
    expect(amountBox(form)).toHaveValue("۶۴۰٬۰۰۰");
  });

  it("SettleDebt_ItemUnticked_LowersTheAmountAndLeavesTheItemOut", async () => {
    const api = profile({
      [`POST ${settlePath}`]: () => json(200, { ...settled, payments: [], remainingDebt: 600000 }),
    });
    const form = await openForm();

    fireEvent.click(within(form).getByRole("checkbox", { name: /اشتراک ماهانه/ }));
    expect(amountBox(form)).toHaveValue("۴۰٬۰۰۰");
    fireEvent.click(within(form).getByRole("button", { name: "تأیید تسویه" }));

    await waitFor(() => expect(api.requestsTo("POST", settlePath)).toHaveLength(1));
    const body = (await api.requestsTo("POST", settlePath)[0]!.json()) as Record<string, unknown>;
    expect(body).toMatchObject({
      amount: "40000",
      method: "Cash",
      referenceNumber: null,
      items: [
        { kind: "CafeOrder", id: cafeDebtItem().id, outstanding: 30000 },
        { kind: "ServiceCharge", id: serviceChargeDebtItem().id, outstanding: 10000 },
      ],
    });
  });

  it("SettleDebt_NothingTicked_CannotBeSent", async () => {
    profile();
    const form = await openForm();

    within(form)
      .getAllByRole("checkbox")
      .forEach((box) => fireEvent.click(box));

    expect(within(form).getByText("دست‌کم یک مورد را انتخاب کنید.")).toBeInTheDocument();
    expect(within(form).getByRole("button", { name: "تأیید تسویه" })).toBeDisabled();
  });

  it("SettleDebt_AmountAboveTheTickedTotal_IsRefusedBeforeSending", async () => {
    const api = profile();
    const form = await openForm();

    fireEvent.change(amountBox(form), { target: { value: "700000" } });
    fireEvent.click(within(form).getByRole("button", { name: "تأیید تسویه" }));

    expect(
      await within(form).findByText("مبلغ از جمع موارد انتخاب‌شده بیشتر است."),
    ).toBeInTheDocument();
    expect(api.requestsTo("POST", settlePath)).toHaveLength(0);
  });

  it("SettleDebt_Confirmed_ShowsWhatEachItemReceivedAfterTheDebtIsCleared", async () => {
    let paid = false;
    profile({
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt(paid ? [] : owed),
      [`POST ${settlePath}`]: () => {
        paid = true;
        return json(200, settled);
      },
    });
    const form = await openForm();

    fireEvent.change(within(form).getByLabelText("روش پرداخت"), { target: { value: "Card" } });
    fireEvent.click(within(form).getByRole("button", { name: "تأیید تسویه" }));

    const done = await screen.findByRole("status", { name: "تسویه ثبت شد" });
    expect(done).toHaveTextContent("تسویه ثبت شد: ۶۴۰٬۰۰۰ تومان (کارت)");
    expect(within(done).getByText("بوفه")).toBeInTheDocument();
    expect(within(done).getByText("اشتراک ماهانه")).toBeInTheDocument();
    expect(within(done).getByText("بدهی این عضو صاف شد.")).toBeInTheDocument();

    // The debt behind it is refetched and empty; the step stays until the desk closes it.
    expect(await screen.findByText("این عضو بدهی ندارد.")).toBeInTheDocument();
    expect(screen.getByRole("status", { name: "تسویه ثبت شد" })).toBeInTheDocument();
    fireEvent.click(within(done).getByRole("button", { name: "بستن" }));
    expect(screen.queryByRole("status", { name: "تسویه ثبت شد" })).not.toBeInTheDocument();
  });

  it("SettleDebt_PartPaid_SaysWhatIsStillOwed", async () => {
    profile({
      [`POST ${settlePath}`]: () =>
        json(200, {
          ...settled,
          amount: 100000,
          payments: [
            { ...settled.payments[0], amount: 30000 },
            { ...settled.payments[1], amount: 10000 },
            { ...settled.payments[2], amount: 60000, outstanding: 540000 },
          ],
          remainingDebt: 540000,
        }),
    });
    const form = await openForm();

    fireEvent.change(amountBox(form), { target: { value: "100000" } });
    fireEvent.click(within(form).getByRole("button", { name: "تأیید تسویه" }));

    const done = await screen.findByRole("status", { name: "تسویه ثبت شد" });
    expect(within(done).getByText("بدهی باقی‌مانده: ۵۴۰٬۰۰۰ تومان")).toBeInTheDocument();
  });

  it("SettleDebt_DebtChangedMeanwhile_ShowsThePersianReasonAndTheNewList", async () => {
    let changed = false;
    const api = profile({
      [`GET /api/members/${reza.id}/debt`]: () => memberDebt(changed ? [debtItem()] : owed),
      [`POST ${settlePath}`]: () => {
        changed = true;
        return problem(409, "Settlements.DebtChanged");
      },
    });
    const form = await openForm();

    fireEvent.click(within(form).getByRole("button", { name: "تأیید تسویه" }));

    expect(
      await within(form).findByText(/بدهی این عضو در این فاصله تغییر کرد/),
    ).toBeInTheDocument();
    // Reloaded: only the plan is left, and the amount follows the new total.
    await waitFor(() => expect(within(form).getAllByRole("checkbox")).toHaveLength(1));
    expect(amountBox(form)).toHaveValue("۶۰۰٬۰۰۰");
    expect(api.requestsTo("POST", settlePath)).toHaveLength(1);
  });

  it("SettleDebt_MemberOwesNothing_OffersNoButton", async () => {
    profile({ [`GET /api/members/${reza.id}/debt`]: () => memberDebt([]) });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("این عضو بدهی ندارد.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "تسویه یکجا" })).not.toBeInTheDocument();
  });
});
