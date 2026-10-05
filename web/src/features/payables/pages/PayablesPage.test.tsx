import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { categoriesPage, electricity, equipment, rent } from "@/test/expenses";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import {
  cancelledCheque,
  futureCheque,
  futureInstallment,
  overdueCheque,
  passedCheque,
  payablesPage,
} from "@/test/payables";
import { renderApp } from "@/test/renderApp";

const totals = { cheques: 80000000, installments: 5000000 };

/** The register answers by status and kind, as the API does; the totals are the same whatever the filter. */
const register = {
  ...signedInHandlers(owner),
  "GET /api/expenses/categories": () => categoriesPage([rent, electricity, equipment]),
  "GET /api/payables": (request: Request) => {
    const query = new URL(request.url).searchParams;
    const byStatus = {
      Pending: [overdueCheque, futureCheque, futureInstallment],
      Paid: [passedCheque],
      Cancelled: [cancelledCheque],
    }[query.get("Status") ?? ""] ?? [
      futureInstallment,
      futureCheque,
      overdueCheque,
      cancelledCheque,
      passedCheque,
    ];
    const kind = query.get("Kind");
    return payablesPage(
      kind === null ? byStatus : byStatus.filter((payable) => payable.kind === kind),
      totals,
    );
  },
};

function rowWith(text: string) {
  const row = screen.getAllByText(text)[0]!.closest("tr");
  if (row === null) {
    throw new Error(`No row shows ${text}`);
  }
  return row;
}

/** The form that holds a given field: the new card, or the one opened under a row. */
function formOf(label: string) {
  const form = screen.getByLabelText(label).closest("form");
  if (form === null) {
    throw new Error(`No form holds ${label}`);
  }
  return form;
}

function lastListQuery(api: ReturnType<typeof mockApi>) {
  const requests = api.requestsTo("GET", "/api/payables");
  return new URL(requests[requests.length - 1]!.url).searchParams;
}

describe("PayablesPage", () => {
  it("PayablesPage_Staff_SeesNoAccessMessageAndNoRequest", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/payables", { session: session() });

    // §1: cheques and instalments are the Owner's, reading included.
    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/payables")).toHaveLength(0);
  });

  it("PayablesPage_Opened_IsTitledChequeAndInstallmentAndShowsTheTotals", async () => {
    const api = mockApi(register);
    renderApp("/payables", { session: session() });

    expect(await screen.findByRole("heading", { name: "چک و قسط" })).toBeInTheDocument();
    expect(await screen.findByLabelText("جمع در انتظار")).toHaveTextContent("۸۵٬۰۰۰٬۰۰۰ تومان");
    expect(screen.getByLabelText("جمع چک‌های در انتظار")).toHaveTextContent("۸۰٬۰۰۰٬۰۰۰ تومان");
    expect(screen.getByLabelText("جمع قسط‌های در انتظار")).toHaveTextContent("۵٬۰۰۰٬۰۰۰ تومان");
    expect(
      screen.getByText(
        /چک و قسط هزینه است: بعد از پاس شدن یا پرداخت، سیستم خودش آن را در هزینه‌ها ثبت می‌کند/,
      ),
    ).toBeInTheDocument();
    expect(lastListQuery(api).get("Status")).toBe("Pending");
    expect(lastListQuery(api).has("Kind")).toBe(false);
  });

  it("PayablesPage_PendingRows_ChequeWaitsForItsDateButAnInstallmentCanBePaidEarly", async () => {
    mockApi(register);
    renderApp("/payables", { session: session() });

    const overdueRow = await waitFor(() => rowWith("تردمیل"));
    expect(within(overdueRow).getByText("سررسید گذشته")).toBeInTheDocument();
    expect(within(overdueRow).getByRole("button", { name: /^پاس شد/ })).toBeInTheDocument();

    // §9: a bank does not pay a cheque before its date.
    const futureRow = rowWith("دوچرخه");
    expect(within(futureRow).queryByRole("button", { name: /^پاس شد/ })).not.toBeInTheDocument();
    expect(within(futureRow).getByRole("button", { name: /^ویرایش/ })).toBeInTheDocument();

    // An instalment can be paid at any time, and says which one it is.
    const installmentRow = rowWith("وام دستگاه");
    expect(within(installmentRow).getByText("قسط ۳ از ۱۲")).toBeInTheDocument();
    expect(within(installmentRow).getByRole("button", { name: /^پرداخت شد/ })).toBeInTheDocument();
  });

  it("PayablesPage_AllTab_PaidCanGoBackAndCancelledHasNoActions", async () => {
    mockApi(register);
    renderApp("/payables?status=all", { session: session() });

    const cancelledRow = await waitFor(() => rowWith("پیش‌پرداخت"));
    expect(within(cancelledRow).getByText("باطل شده")).toBeInTheDocument();
    expect(within(cancelledRow).queryByRole("button")).not.toBeInTheDocument();

    const passedRow = rowWith("پیش‌قسط");
    expect(within(passedRow).getByText("پاس شد")).toBeInTheDocument();
    expect(within(passedRow).getByRole("link", { name: "در هزینه‌ها ثبت شد" })).toHaveAttribute(
      "href",
      "/expenses",
    );
    expect(
      within(passedRow).getByRole("button", { name: /^برگشت به در انتظار/ }),
    ).toBeInTheDocument();
  });

  it("PayablesPage_ChooseTheKind_PutsItInTheUrlAndAsksTheApi", async () => {
    const api = mockApi(register);
    const { router } = renderApp("/payables", { session: session() });

    await waitFor(() => rowWith("تردمیل"));
    fireEvent.click(
      within(screen.getByRole("group", { name: "نوع" })).getByRole("button", { name: "قسط" }),
    );

    await waitFor(() => expect(lastListQuery(api).get("Kind")).toBe("Installment"));
    expect(router.state.location.search).toBe("?kind=Installment");
    await waitFor(() => expect(screen.queryByText("تردمیل")).not.toBeInTheDocument());
    expect(screen.getByText("وام دستگاه")).toBeInTheDocument();
  });

  it("PayablesPage_RegisterCheque_SendsKindCategoryAndNoInstallmentNumbers", async () => {
    const api = mockApi({
      ...register,
      "POST /api/payables": () => json(201, { ...futureCheque, id: "new" }),
    });
    renderApp("/payables", { session: session() });

    await waitFor(() => rowWith("تردمیل"));
    fireEvent.click(screen.getByRole("button", { name: "ثبت چک یا قسط" }));

    const form = formOf("در وجه");
    fireEvent.change(within(form).getByLabelText("مبلغ (تومان)"), {
      target: { value: "۲۵۰۰۰۰۰۰" },
    });
    fireEvent.change(within(form).getByLabelText("تاریخ چک"), { target: { value: "۱۴۰۵/۰۸/۱۵" } });
    fireEvent.change(within(form).getByLabelText("در وجه"), { target: { value: " فروشگاه " } });
    fireEvent.change(within(form).getByLabelText("دسته‌بندی هزینه"), {
      target: { value: equipment.id },
    });
    fireEvent.change(within(form).getByLabelText("شرح"), { target: { value: "پرس سینه" } });
    fireEvent.click(within(form).getByRole("button", { name: "ثبت" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "چک به مبلغ ۲۵٬۰۰۰٬۰۰۰ تومان ثبت شد.",
    );
    const [request] = api.requestsTo("POST", "/api/payables");
    expect(await request!.json()).toEqual({
      kind: "Cheque",
      amount: "25000000",
      dueDate: "2026-11-06",
      payee: "فروشگاه",
      description: "پرس سینه",
      categoryId: equipment.id,
      installmentNumber: null,
      installmentCount: null,
    });
  });

  it("PayablesPage_RegisterInstallment_AsksForItsNumbersAndSendsThem", async () => {
    const api = mockApi({
      ...register,
      "POST /api/payables": () => json(201, { ...futureInstallment, id: "new" }),
    });
    renderApp("/payables", { session: session() });

    await waitFor(() => rowWith("تردمیل"));
    fireEvent.click(screen.getByRole("button", { name: "ثبت چک یا قسط" }));

    const form = formOf("در وجه");
    fireEvent.change(within(form).getByLabelText("نوع"), { target: { value: "Installment" } });
    expect(await within(form).findByLabelText("پرداخت به")).toBeInTheDocument();
    fireEvent.change(within(form).getByLabelText("مبلغ (تومان)"), {
      target: { value: "5000000" },
    });
    fireEvent.change(within(form).getByLabelText("تاریخ سررسید قسط"), {
      target: { value: "۱۴۰۵/۰۸/۰۱" },
    });
    fireEvent.change(within(form).getByLabelText("پرداخت به"), { target: { value: "بانک ملت" } });
    fireEvent.change(within(form).getByLabelText("دسته‌بندی هزینه"), {
      target: { value: equipment.id },
    });
    fireEvent.change(within(form).getByLabelText("شرح"), { target: { value: "وام دستگاه" } });
    fireEvent.click(within(form).getByRole("button", { name: "ثبت" }));

    // «قسط n از N» is required for an instalment.
    expect(await within(form).findByText("شمارهٔ قسط را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/payables")).toHaveLength(0);

    fireEvent.change(within(form).getByLabelText("شمارهٔ قسط"), { target: { value: "۳" } });
    fireEvent.change(within(form).getByLabelText("از چند قسط"), { target: { value: "12" } });
    fireEvent.click(within(form).getByRole("button", { name: "ثبت" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "قسط به مبلغ ۵٬۰۰۰٬۰۰۰ تومان ثبت شد.",
    );
    const [request] = api.requestsTo("POST", "/api/payables");
    expect(await request!.json()).toMatchObject({
      kind: "Installment",
      payee: "بانک ملت",
      installmentNumber: 3,
      installmentCount: 12,
    });
  });

  it("PayablesPage_RegisterEmpty_IsRefusedBeforeSending", async () => {
    const api = mockApi(register);
    renderApp("/payables", { session: session() });

    await waitFor(() => rowWith("تردمیل"));
    fireEvent.click(screen.getByRole("button", { name: "ثبت چک یا قسط" }));
    const form = formOf("در وجه");
    fireEvent.click(within(form).getByRole("button", { name: "ثبت" }));

    expect(await within(form).findByText("مبلغ را وارد کنید.")).toBeInTheDocument();
    expect(within(form).getByText("تاریخ را وارد کنید.")).toBeInTheDocument();
    expect(within(form).getByText("نام گیرنده را وارد کنید.")).toBeInTheDocument();
    expect(within(form).getByText("شرح را وارد کنید.")).toBeInTheDocument();
    expect(within(form).getByText("دسته‌بندی هزینه را انتخاب کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/payables")).toHaveLength(0);
  });

  it("PayablesPage_EditInstallment_SendsTheChangesWithTheVersionItWasReadAt", async () => {
    const api = mockApi({
      ...register,
      [`PUT /api/payables/${futureInstallment.id}`]: () =>
        json(200, { ...futureInstallment, installmentNumber: 4, version: 9 }),
    });
    renderApp("/payables", { session: session() });

    const row = await waitFor(() => rowWith("وام دستگاه"));
    fireEvent.click(within(row).getByRole("button", { name: /^ویرایش/ }));

    const form = formOf("پرداخت به");
    expect(within(form).getByLabelText("شمارهٔ قسط")).toHaveValue("۳");
    fireEvent.change(within(form).getByLabelText("شمارهٔ قسط"), { target: { value: "4" } });
    fireEvent.click(within(form).getByRole("button", { name: "ذخیرهٔ تغییرات" }));

    expect(await screen.findByRole("status")).toHaveTextContent("قسط ویرایش شد.");
    const [request] = api.requestsTo("PUT", `/api/payables/${futureInstallment.id}`);
    expect(await request!.json()).toEqual({
      kind: "Installment",
      amount: "5000000",
      dueDate: "2099-02-01",
      payee: "بانک ملت",
      description: "وام دستگاه",
      categoryId: equipment.id,
      installmentNumber: 4,
      installmentCount: 12,
      version: 8,
    });
  });

  it("PayablesPage_Pay_AsksFirstThenSendsAndSaysTheExpenseIsRecorded", async () => {
    const api = mockApi({
      ...register,
      [`POST /api/payables/${overdueCheque.id}/pay`]: () =>
        json(200, { ...overdueCheque, status: "Paid" }),
    });
    renderApp("/payables", { session: session() });

    const row = await waitFor(() => rowWith("تردمیل"));
    fireEvent.click(within(row).getByRole("button", { name: /^پاس شد/ }));

    // Nothing is sent until it is confirmed.
    expect(screen.getByText(/با تاریخ امروز در هزینه‌ها ثبت می‌شود/)).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/payables/${overdueCheque.id}/pay`)).toHaveLength(0);

    fireEvent.click(screen.getByRole("button", { name: "تأیید پاس شد" }));

    expect(await screen.findByRole("status")).toHaveTextContent("چک پاس شد و در هزینه‌ها ثبت شد.");
    expect(api.requestsTo("POST", `/api/payables/${overdueCheque.id}/pay`)).toHaveLength(1);
  });

  it("PayablesPage_PayRefused_ShowsTheMessage", async () => {
    mockApi({
      ...register,
      [`POST /api/payables/${overdueCheque.id}/pay`]: () =>
        problem(422, "Payables.AlreadyCancelled"),
    });
    renderApp("/payables", { session: session() });

    const row = await waitFor(() => rowWith("تردمیل"));
    fireEvent.click(within(row).getByRole("button", { name: /^پاس شد/ }));
    fireEvent.click(screen.getByRole("button", { name: "تأیید پاس شد" }));

    expect(
      await screen.findByText("این مورد باطل شده است و دیگر تغییر نمی‌کند."),
    ).toBeInTheDocument();
  });

  it("PayablesPage_Revert_RequiresAReasonAndSendsIt", async () => {
    const api = mockApi({
      ...register,
      [`POST /api/payables/${passedCheque.id}/revert`]: () =>
        json(200, { ...passedCheque, status: "Pending" }),
    });
    renderApp("/payables?status=Paid", { session: session() });

    const row = await waitFor(() => rowWith("پیش‌قسط"));
    fireEvent.click(within(row).getByRole("button", { name: /^برگشت به در انتظار/ }));

    expect(screen.getByText(/با همین دلیل باطل می‌شود/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "تأیید برگشت" }));
    expect(await screen.findByText("دلیل برگشت را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/payables/${passedCheque.id}/revert`)).toHaveLength(0);

    fireEvent.change(screen.getByLabelText("دلیل برگشت"), { target: { value: "اشتباهی زده شد" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید برگشت" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "چک به در انتظار برگشت و هزینه‌اش باطل شد.",
    );
    const [request] = api.requestsTo("POST", `/api/payables/${passedCheque.id}/revert`);
    expect(await request!.json()).toEqual({ reason: "اشتباهی زده شد" });
  });

  it("PayablesPage_Cancel_RequiresAReasonAndSendsIt", async () => {
    const api = mockApi({
      ...register,
      [`POST /api/payables/${futureCheque.id}/cancel`]: () =>
        json(200, { ...futureCheque, status: "Cancelled" }),
    });
    renderApp("/payables", { session: session() });

    const row = await waitFor(() => rowWith("دوچرخه"));
    fireEvent.click(within(row).getByRole("button", { name: /^ابطال/ }));

    fireEvent.click(screen.getByRole("button", { name: "تأیید ابطال" }));
    expect(await screen.findByText("دلیل ابطال را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/payables/${futureCheque.id}/cancel`)).toHaveLength(0);

    fireEvent.change(screen.getByLabelText("دلیل ابطال"), { target: { value: "پس گرفته شد" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید ابطال" }));

    expect(await screen.findByRole("status")).toHaveTextContent("چک باطل شد.");
    const [request] = api.requestsTo("POST", `/api/payables/${futureCheque.id}/cancel`);
    expect(await request!.json()).toEqual({ reason: "پس گرفته شد" });
  });

  it("PayablesPage_EmptyTab_SaysSo", async () => {
    mockApi({ ...register, "GET /api/payables": () => payablesPage([]) });
    renderApp("/payables?status=Cancelled", { session: session() });

    expect(await screen.findByText("مورد باطل‌شده‌ای نیست.")).toBeInTheDocument();
  });
});
