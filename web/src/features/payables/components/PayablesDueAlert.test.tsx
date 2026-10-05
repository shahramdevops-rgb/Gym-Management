import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { paths } from "@/app/paths";
import type { PayableDueSoon } from "@/features/payables/api";
import { json, mockApi, owner, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

const dueSoonPath = "/api/payables/due-soon";

const chequeInThreeDays: PayableDueSoon = {
  payableId: "0199a000-0000-7000-8000-0000000000d1",
  kind: "Cheque",
  payee: "فروشگاه تجهیزات",
  amount: 50000000,
  dueDate: "2026-09-21",
  installmentNumber: null,
  installmentCount: null,
  daysLeft: 3,
};

const installmentToday: PayableDueSoon = {
  payableId: "0199a000-0000-7000-8000-0000000000d2",
  kind: "Installment",
  payee: "بانک ملت",
  amount: 5000000,
  dueDate: "2026-09-18",
  installmentNumber: 4,
  installmentCount: 12,
  daysLeft: 0,
};

const overdueCheque: PayableDueSoon = {
  ...chequeInThreeDays,
  payableId: "0199a000-0000-7000-8000-0000000000d3",
  payee: "تعمیرگاه",
  dueDate: "2026-09-16",
  daysLeft: -2,
};

function dueSoon(items: PayableDueSoon[]) {
  return {
    ...signedInHandlers(owner),
    [`GET ${dueSoonPath}`]: () => json(200, { today: "2026-09-18", items }),
  };
}

/** The pill in the header: the one button whose name starts with the alert's hidden label. */
async function findAlert() {
  return screen.findByRole("button", { name: /^چک و قسط نزدیک سررسید/ });
}

describe("PayablesDueAlert", () => {
  it("PayablesDueAlert_OneClose_ShowsItsKindPayeeAndDaysLeftInTheHeader", async () => {
    mockApi(dueSoon([chequeInThreeDays]));

    renderApp(paths.changePassword, { session: session() });

    const alert = await findAlert();
    expect(alert.closest("header")).not.toBeNull();
    expect(alert).toHaveTextContent("چک");
    expect(alert).toHaveTextContent("فروشگاه تجهیزات");
    expect(alert).toHaveTextContent("۳ روز دیگر");
    expect(alert).not.toHaveTextContent("+");
  });

  it("PayablesDueAlert_SeveralClose_NamesTheNearestAndCountsTheOthers", async () => {
    mockApi(dueSoon([installmentToday, chequeInThreeDays]));

    renderApp(paths.changePassword, { session: session() });

    const alert = await findAlert();
    expect(alert).toHaveTextContent("قسط ۴ از ۱۲");
    expect(alert).toHaveTextContent("امروز");
    expect(within(alert).getByLabelText("و ۱ مورد دیگر")).toHaveTextContent("+۱");
  });

  it("PayablesDueAlert_PastItsDate_IsRedAndSaysHowLongAgo", async () => {
    mockApi(dueSoon([overdueCheque, chequeInThreeDays]));

    renderApp(paths.changePassword, { session: session() });

    const alert = await findAlert();
    expect(alert).toHaveTextContent("۲ روز گذشته");
    expect(alert.className).toContain("bg-destructive/10");
  });

  it("PayablesDueAlert_Clicked_ListsEveryOneWithAmountAndALinkToTheRegister", async () => {
    mockApi(dueSoon([overdueCheque, installmentToday, chequeInThreeDays]));

    renderApp(paths.changePassword, { session: session() });

    fireEvent.click(await findAlert());

    const dialog = await screen.findByRole("dialog", { name: "چک و قسط نزدیک سررسید" });
    const rows = within(dialog).getAllByRole("listitem");
    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent("تعمیرگاه");
    expect(rows[0]).toHaveTextContent("۲ روز گذشته");
    expect(rows[1]).toHaveTextContent("بانک ملت");
    expect(rows[1]).toHaveTextContent("امروز");
    expect(rows[2]).toHaveTextContent("۳ روز دیگر");
    expect(rows[2]).toHaveTextContent("۵۰٬۰۰۰٬۰۰۰");
    expect(within(dialog).getByText(/تا ۵ روز دیگر/)).toBeInTheDocument();
    expect(within(dialog).getByRole("link", { name: "همهٔ چک‌ها و قسط‌ها" })).toHaveAttribute(
      "href",
      paths.payables,
    );
  });

  it("PayablesDueAlert_NothingClose_ShowsNothing", async () => {
    const api = mockApi(dueSoon([]));

    renderApp(paths.changePassword, { session: session() });

    await waitFor(() => expect(api.requestsTo("GET", dueSoonPath)).toHaveLength(1));
    expect(
      screen.queryByRole("button", { name: /^چک و قسط نزدیک سررسید/ }),
    ).not.toBeInTheDocument();
  });

  it("PayablesDueAlert_Staff_NeverAsks", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp(paths.changePassword, { session: session() });

    await screen.findByText(staffUser.fullName);
    expect(api.requestsTo("GET", dueSoonPath)).toHaveLength(0);
    expect(api.unexpected).toEqual([]);
  });
});
