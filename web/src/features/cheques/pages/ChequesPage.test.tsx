import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import {
  cancelledCheque,
  chequesPage,
  futureCheque,
  overdueCheque,
  passedCheque,
} from "@/test/cheques";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

/** The register answers by tab, as the API does; the total is every pending cheque whatever the tab. */
const register = {
  ...signedInHandlers(owner),
  "GET /api/cheques": (request: Request) => {
    const status = new URL(request.url).searchParams.get("Status");
    const items = {
      Pending: [overdueCheque, futureCheque],
      Passed: [passedCheque],
      Cancelled: [cancelledCheque],
    }[status ?? ""] ?? [futureCheque, overdueCheque, cancelledCheque, passedCheque];
    return chequesPage(items, 80000000);
  },
};

function rowWith(text: string) {
  const row = screen.getAllByText(text)[0]!.closest("tr");
  if (row === null) {
    throw new Error(`No row shows ${text}`);
  }
  return row;
}

/** The form that holds a given field: the new-cheque card, or the one opened under a row. */
function formOf(label: string) {
  const form = screen.getByLabelText(label).closest("form");
  if (form === null) {
    throw new Error(`No form holds ${label}`);
  }
  return form;
}

function lastListQuery(api: ReturnType<typeof mockApi>) {
  const requests = api.requestsTo("GET", "/api/cheques");
  return new URL(requests[requests.length - 1]!.url).searchParams;
}

describe("ChequesPage", () => {
  it("ChequesPage_Staff_SeesNoAccessMessageAndNoRequest", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/cheques", { session: session() });

    // §1: cheques are the Owner's, reading included.
    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/cheques")).toHaveLength(0);
  });

  it("ChequesPage_Opened_AsksForPendingAndShowsThePendingTotal", async () => {
    const api = mockApi(register);
    renderApp("/cheques", { session: session() });

    expect(await screen.findByLabelText("جمع چک‌های در انتظار")).toHaveTextContent(
      "۸۰٬۰۰۰٬۰۰۰ تومان",
    );
    expect(lastListQuery(api).get("Status")).toBe("Pending");
    expect(screen.getByRole("button", { name: "در انتظار" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("ChequesPage_PendingRows_OverdueIsMarkedAndOnlyADueChequeCanBePassed", async () => {
    mockApi(register);
    renderApp("/cheques", { session: session() });

    const overdueRow = await waitFor(() => rowWith("قسط اول تردمیل"));
    expect(within(overdueRow).getByText("سررسید گذشته")).toBeInTheDocument();
    expect(within(overdueRow).getByRole("button", { name: /^پاس شد/ })).toBeInTheDocument();

    // §9: a bank does not pay a cheque before its date, so there is nothing to mark yet.
    const futureRow = rowWith("قسط دوم دوچرخه");
    expect(within(futureRow).getByText("در انتظار")).toBeInTheDocument();
    expect(within(futureRow).queryByRole("button", { name: /^پاس شد/ })).not.toBeInTheDocument();
    expect(within(futureRow).getByRole("button", { name: /^ویرایش/ })).toBeInTheDocument();
    expect(within(futureRow).getByRole("button", { name: /^ابطال/ })).toBeInTheDocument();
  });

  it("ChequesPage_AllTab_ShowsPassedAndCancelledMarkedWithoutActions", async () => {
    const api = mockApi(register);
    renderApp("/cheques?status=all", { session: session() });

    const cancelledRow = await waitFor(() => rowWith("پیش‌پرداخت"));
    expect(
      api
        .requestsTo("GET", "/api/cheques")
        .every((r) => !new URL(r.url).searchParams.has("Status")),
    ).toBe(true);
    expect(within(cancelledRow).getByText("باطل شده")).toBeInTheDocument();
    expect(within(cancelledRow).getByText(/از فروشنده پس گرفته شد/)).toBeInTheDocument();
    // Passed and cancelled are final (§9): nothing to do on their rows.
    expect(within(cancelledRow).queryByRole("button")).not.toBeInTheDocument();
    const passedRow = rowWith("قسط صفر");
    expect(within(passedRow).getByText("پاس شد")).toBeInTheDocument();
    expect(within(passedRow).queryByRole("button")).not.toBeInTheDocument();
  });

  it("ChequesPage_ChooseATab_PutsItInTheUrlAndAsksTheApi", async () => {
    const api = mockApi(register);
    const { router } = renderApp("/cheques", { session: session() });

    await waitFor(() => rowWith("قسط اول تردمیل"));
    fireEvent.click(screen.getByRole("button", { name: "پاس شد" }));

    await waitFor(() => expect(lastListQuery(api).get("Status")).toBe("Passed"));
    expect(router.state.location.search).toBe("?status=Passed");
    expect(await screen.findByText("قسط صفر")).toBeInTheDocument();
  });

  it("ChequesPage_Register_SendsTheAmountAsDecimalTextAndTheIsoDate", async () => {
    const api = mockApi({
      ...register,
      "POST /api/cheques": () => json(201, { ...futureCheque, id: "new" }),
    });
    renderApp("/cheques", { session: session() });

    await waitFor(() => rowWith("قسط اول تردمیل"));
    fireEvent.click(screen.getByRole("button", { name: "ثبت چک" }));

    const form = formOf("در وجه");
    fireEvent.change(within(form).getByLabelText("مبلغ (تومان)"), {
      target: { value: "۲۵۰۰۰۰۰۰" },
    });
    fireEvent.change(within(form).getByLabelText("تاریخ چک"), { target: { value: "۱۴۰۵/۰۸/۱۵" } });
    fireEvent.change(within(form).getByLabelText("در وجه"), { target: { value: " فروشگاه " } });
    fireEvent.change(within(form).getByLabelText("شرح"), { target: { value: "قسط سوم" } });
    fireEvent.click(within(form).getByRole("button", { name: "ثبت چک" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "چک به مبلغ ۲۵٬۰۰۰٬۰۰۰ تومان ثبت شد.",
    );
    const [request] = api.requestsTo("POST", "/api/cheques");
    expect(await request!.json()).toEqual({
      amount: "25000000",
      dueDate: "2026-11-06",
      payee: "فروشگاه",
      description: "قسط سوم",
    });
  });

  it("ChequesPage_RegisterEmpty_IsRefusedBeforeSending", async () => {
    const api = mockApi(register);
    renderApp("/cheques", { session: session() });

    await waitFor(() => rowWith("قسط اول تردمیل"));
    fireEvent.click(screen.getByRole("button", { name: "ثبت چک" }));
    const form = formOf("در وجه");
    fireEvent.click(within(form).getByRole("button", { name: "ثبت چک" }));

    expect(await within(form).findByText("مبلغ را وارد کنید.")).toBeInTheDocument();
    expect(within(form).getByText("تاریخ چک را وارد کنید.")).toBeInTheDocument();
    expect(within(form).getByText("نام گیرنده (در وجه) را وارد کنید.")).toBeInTheDocument();
    expect(within(form).getByText("شرح چک را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/cheques")).toHaveLength(0);
  });

  it("ChequesPage_Edit_SendsTheChangesWithTheVersionItWasReadAt", async () => {
    const api = mockApi({
      ...register,
      [`PUT /api/cheques/${futureCheque.id}`]: () =>
        json(200, { ...futureCheque, amount: 35000000, version: 6 }),
    });
    renderApp("/cheques", { session: session() });

    const row = await waitFor(() => rowWith("قسط دوم دوچرخه"));
    fireEvent.click(within(row).getByRole("button", { name: /^ویرایش/ }));

    const form = formOf("در وجه");
    expect(within(form).getByLabelText("مبلغ (تومان)")).toHaveValue("۳۰٬۰۰۰٬۰۰۰");
    fireEvent.change(within(form).getByLabelText("مبلغ (تومان)"), {
      target: { value: "35,000,000" },
    });
    fireEvent.click(within(form).getByRole("button", { name: "ذخیرهٔ تغییرات" }));

    expect(await screen.findByRole("status")).toHaveTextContent("چک ویرایش شد.");
    const [request] = api.requestsTo("PUT", `/api/cheques/${futureCheque.id}`);
    expect(await request!.json()).toEqual({
      amount: "35000000",
      dueDate: "2099-01-01",
      payee: "تعمیرگاه",
      description: "قسط دوم دوچرخه",
      version: 5,
    });
  });

  it("ChequesPage_Pass_AsksFirstThenSendsAndRemindsToRecordTheExpense", async () => {
    const api = mockApi({
      ...register,
      [`POST /api/cheques/${overdueCheque.id}/pass`]: () =>
        json(200, { ...overdueCheque, status: "Passed" }),
    });
    renderApp("/cheques", { session: session() });

    const row = await waitFor(() => rowWith("قسط اول تردمیل"));
    fireEvent.click(within(row).getByRole("button", { name: /^پاس شد/ }));

    // Final, so nothing is sent until it is confirmed; the expense is the Owner's to record.
    const warning = screen.getByText(/قطعی است و دیگر برنمی‌گردد/);
    expect(within(warning).getByRole("link", { name: "هزینه‌ها" })).toHaveAttribute(
      "href",
      "/expenses",
    );
    expect(api.requestsTo("POST", `/api/cheques/${overdueCheque.id}/pass`)).toHaveLength(0);

    fireEvent.click(screen.getByRole("button", { name: "تأیید پاس شدن" }));

    expect(await screen.findByRole("status")).toHaveTextContent("چک پاس شد.");
    expect(api.requestsTo("POST", `/api/cheques/${overdueCheque.id}/pass`)).toHaveLength(1);
  });

  it("ChequesPage_PassRefused_ShowsTheMessage", async () => {
    mockApi({
      ...register,
      [`POST /api/cheques/${overdueCheque.id}/pass`]: () =>
        problem(422, "Cheques.AlreadyCancelled"),
    });
    renderApp("/cheques", { session: session() });

    const row = await waitFor(() => rowWith("قسط اول تردمیل"));
    fireEvent.click(within(row).getByRole("button", { name: /^پاس شد/ }));
    fireEvent.click(screen.getByRole("button", { name: "تأیید پاس شدن" }));

    expect(
      await screen.findByText("این چک باطل شده است و دیگر تغییر نمی‌کند."),
    ).toBeInTheDocument();
  });

  it("ChequesPage_Cancel_RequiresAReasonAndSendsIt", async () => {
    const api = mockApi({
      ...register,
      [`POST /api/cheques/${futureCheque.id}/cancel`]: () =>
        json(200, { ...futureCheque, status: "Cancelled" }),
    });
    renderApp("/cheques", { session: session() });

    const row = await waitFor(() => rowWith("قسط دوم دوچرخه"));
    fireEvent.click(within(row).getByRole("button", { name: /^ابطال/ }));

    fireEvent.click(screen.getByRole("button", { name: "تأیید ابطال" }));
    expect(await screen.findByText("دلیل ابطال را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/cheques/${futureCheque.id}/cancel`)).toHaveLength(0);

    fireEvent.change(screen.getByLabelText("دلیل ابطال"), { target: { value: "پس گرفته شد" } });
    fireEvent.click(screen.getByRole("button", { name: "تأیید ابطال" }));

    expect(await screen.findByRole("status")).toHaveTextContent("چک باطل شد.");
    const [request] = api.requestsTo("POST", `/api/cheques/${futureCheque.id}/cancel`);
    expect(await request!.json()).toEqual({ reason: "پس گرفته شد" });
  });

  it("ChequesPage_EmptyTab_SaysSo", async () => {
    mockApi({ ...register, "GET /api/cheques": () => chequesPage([], 0) });
    renderApp("/cheques?status=Cancelled", { session: session() });

    expect(await screen.findByText("چک باطل‌شده‌ای نیست.")).toBeInTheDocument();
  });
});
