import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import {
  gymToday,
  jalaliMonthNames,
  jalaliPartsOf,
  jalaliToIso,
  toPersianDigits,
} from "@/lib/format";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

import type { SmsCredit, SmsMessage } from "../api";

const failedBirthday: SmsMessage = {
  id: "01990000-0000-7000-8000-000000000001",
  kind: "Birthday",
  status: "Failed",
  recipient: "+989121234567",
  memberId: "01990000-0000-7000-8000-0000000000a1",
  memberName: "سارا محمدی",
  payableId: null,
  text: "سارا محمدی عزیز، امروز ۱۴۰۵/۰۷/۱۶ روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد",
  attempts: 1,
  errorCode: 411,
  costToman: null,
  delivery: null,
  createdAt: "2026-10-08T06:30:00Z",
  lastAttemptAt: "2026-10-08T06:30:00Z",
  sentAt: null,
};

const unknownExpiring: SmsMessage = {
  ...failedBirthday,
  id: "01990000-0000-7000-8000-000000000002",
  kind: "SubscriptionExpiring",
  status: "Unknown",
  memberName: "مینا کریمی",
  text: "مینا کریمی عزیز، اشتراک شما در باشگاه پاسارگاد ۱۴۰۵/۰۷/۲۰ به پایان می‌رسد.",
  errorCode: null,
};

const sentCheque: SmsMessage = {
  ...failedBirthday,
  id: "01990000-0000-7000-8000-000000000003",
  kind: "PayableDue",
  status: "Sent",
  recipient: "+989351112233",
  memberId: null,
  memberName: null,
  payableId: "01990000-0000-7000-8000-0000000000b1",
  text: "یادآوری چک: ۱۲٬۵۰۰٬۰۰۰ تومان، سررسید ۱۴۰۵/۰۷/۲۰، به فروشگاه تجهیزات ورزشی",
  errorCode: null,
  costToman: 135,
  delivery: "Delivered",
  sentAt: "2026-10-08T06:30:00Z",
};

function messagesPage(items: SmsMessage[], totalCostToman = 135) {
  return json(200, { items, page: 1, pageSize: 20, totalCount: items.length, totalCostToman });
}

const credit: SmsCredit = { isTestMode: false, remainingToman: 125000, creditUsedUp: false };

function ownerWith(extra: Record<string, Handler> = {}) {
  return mockApi({
    ...signedInHandlers(owner),
    "GET /api/sms/messages": () => messagesPage([failedBirthday, unknownExpiring, sentCheque]),
    "GET /api/sms/credit": () => json(200, credit),
    ...extra,
  });
}

function rowOf(name: string) {
  const row = screen.getAllByText(name)[0]!.closest("tr");
  if (row === null) {
    throw new Error(`No row shows ${name}`);
  }
  return row;
}

function thisMonth() {
  const today = jalaliPartsOf(gymToday())!;

  return {
    from: jalaliToIso(today.year, today.month, 1)!,
    name: `${jalaliMonthNames[today.month - 1]} ${toPersianDigits(today.year)}`,
  };
}

describe("SmsMessagesPage", () => {
  // ---- The list ----

  it("SmsMessagesPage_NoDatesChosen_OpensOnTheCurrentJalaliMonthWithItsCost", async () => {
    const api = ownerWith();

    renderApp("/sms", { session: session() });

    expect(await screen.findByText(`هزینهٔ پیامک‌های ${thisMonth().name}`)).toBeInTheDocument();
    expect(screen.getByLabelText("هزینهٔ پیامک‌ها")).toHaveTextContent("۱۳۵ تومان");
    const url = new URL(api.requestsTo("GET", "/api/sms/messages")[0]!.url);
    expect(url.searchParams.get("From")).toBe(thisMonth().from);
    expect(url.searchParams.get("To")).toBeNull();
  });

  it("SmsMessagesPage_Rows_ShowKindRecipientStatusAndWhatWasSent", async () => {
    ownerWith();

    renderApp("/sms", { session: session() });

    await screen.findByText("سارا محمدی");
    const row = rowOf("سارا محمدی");
    expect(within(row).getByText("تولد")).toBeInTheDocument();
    expect(within(row).getByText("ناموفق")).toBeInTheDocument();
    expect(within(row).getByText("کد ۴۱۱ — شماره نامعتبر است")).toBeInTheDocument();
    expect(within(row).getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toBeInTheDocument();
    expect(
      within(row).getByText(
        "سارا محمدی عزیز، امروز ۱۴۰۵/۰۷/۱۶ روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد",
      ),
    ).toBeInTheDocument();
  });

  it("SmsMessagesPage_ChequesMessage_IsTheOwnersWithItsCostAndDelivery", async () => {
    ownerWith();

    renderApp("/sms", { session: session() });

    await screen.findByText("مالک");
    const row = rowOf("مالک");
    expect(within(row).getByText("چک و قسط")).toBeInTheDocument();
    expect(within(row).getByText("ارسال شد")).toBeInTheDocument();
    expect(within(row).getByText("به گوشی رسید")).toBeInTheDocument();
    expect(within(row).getByText("۱۳۵ تومان")).toBeInTheDocument();
    expect(within(row).queryByRole("button", { name: /ارسال دوباره/ })).not.toBeInTheDocument();
    expect(within(row).queryByText("هزینه برگشت داده شد")).not.toBeInTheDocument();
  });

  it.each([
    ["BlockedByReceiver", "گیرنده مسدود کرده"],
    ["Cancelled", "کاوه‌نگار لغو کرد"],
  ] as const)("SmsMessagesPage_%sMessage_SaysTheCostWasGivenBack", async (delivery, label) => {
    // §10: Kavenegar gives the cost back, and the API keeps it as 0.
    ownerWith({
      "GET /api/sms/messages": () => messagesPage([{ ...sentCheque, delivery, costToman: 0 }], 0),
    });

    renderApp("/sms", { session: session() });

    await screen.findByText("مالک");
    const row = rowOf("مالک");
    expect(within(row).getByText(label)).toBeInTheDocument();
    expect(within(row).getByText("۰ تومان")).toBeInTheDocument();
    expect(within(row).getByText("هزینه برگشت داده شد")).toBeInTheDocument();
  });

  it("SmsMessagesPage_LineWithoutAccess_ShowsWhatKavenegarsCodeMeans", async () => {
    ownerWith({
      "GET /api/sms/messages": () => messagesPage([{ ...failedBirthday, errorCode: 427 }], 0),
    });

    renderApp("/sms", { session: session() });

    await screen.findByText("سارا محمدی");
    expect(
      within(rowOf("سارا محمدی")).getByText("کد ۴۲۷ — این خط برای حساب دسترسی ندارد"),
    ).toBeInTheDocument();
  });

  it("SmsMessagesPage_KindAndStatus_AreSentAndTheMonthStays", async () => {
    const api = ownerWith();

    renderApp("/sms", { session: session() });
    await screen.findByText("سارا محمدی");

    fireEvent.change(screen.getByLabelText("نوع"), { target: { value: "Birthday" } });
    fireEvent.change(screen.getByLabelText("وضعیت"), { target: { value: "Failed" } });

    await waitFor(() => {
      const last = api.requestsTo("GET", "/api/sms/messages").at(-1)!;
      const url = new URL(last.url);
      expect(url.searchParams.get("Kind")).toBe("Birthday");
      expect(url.searchParams.get("Status")).toBe("Failed");
      expect(url.searchParams.get("From")).toBe(thisMonth().from);
    });
    expect(screen.getByText("هزینهٔ پیامک‌های این فیلتر")).toBeInTheDocument();
  });

  it("SmsMessagesPage_NothingInTheRange_SaysSo", async () => {
    ownerWith({ "GET /api/sms/messages": () => messagesPage([], 0) });

    renderApp("/sms", { session: session() });

    expect(await screen.findByText("در این بازه پیامکی نیست.")).toBeInTheDocument();
  });

  // ---- The credit ----

  it("SmsMessagesPage_CreditUsedUp_Warns", async () => {
    ownerWith({
      "GET /api/sms/credit": () => json(200, { ...credit, creditUsedUp: true }),
    });

    renderApp("/sms", { session: session() });

    expect(await screen.findByRole("alert")).toHaveTextContent(/اعتبار پنل پیامک تمام شده/);
  });

  it("SmsMessagesPage_Credit_ShowsTheRemaining", async () => {
    ownerWith();

    renderApp("/sms", { session: session() });

    expect(await screen.findByText("۱۲۵٬۰۰۰ تومان")).toBeInTheDocument();
  });

  // ---- Resend ----

  it("SmsMessagesPage_ResendFailed_AsksThenSendsAndSaysSo", async () => {
    const api = ownerWith({
      [`POST /api/sms/messages/${failedBirthday.id}/resend`]: () =>
        json(200, { ...failedBirthday, status: "Sent", errorCode: null, costToman: 135 }),
    });

    renderApp("/sms", { session: session() });
    await screen.findByText("سارا محمدی");

    fireEvent.click(
      screen.getByRole("button", { name: "ارسال دوباره پیامک تولد برای سارا محمدی" }),
    );
    expect(screen.getByText(/همین پیامک، با همان متن و به همان شماره/)).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/sms/messages/${failedBirthday.id}/resend`)).toHaveLength(0);

    fireEvent.click(screen.getByRole("button", { name: "بفرست" }));

    expect(
      await screen.findByText("پیامک تولد برای سارا محمدی دوباره فرستاده شد."),
    ).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/sms/messages/${failedBirthday.id}/resend`)).toHaveLength(1);
    // The list and the credit are asked again: the row and the total have changed.
    await waitFor(() => expect(api.requestsTo("GET", "/api/sms/messages")).toHaveLength(2));
    expect(api.requestsTo("GET", "/api/sms/credit")).toHaveLength(2);
  });

  it("SmsMessagesPage_ResendUnknown_WarnsItMayHaveArrived", async () => {
    ownerWith();

    renderApp("/sms", { session: session() });
    await screen.findByText("مینا کریمی");

    fireEvent.click(
      screen.getByRole("button", { name: "ارسال دوباره پیامک پایان اشتراک برای مینا کریمی" }),
    );

    expect(screen.getByText(/ممکن است رسیده باشد/)).toBeInTheDocument();
  });

  it("SmsMessagesPage_ResendFailsAgain_SaysItWasNotSent", async () => {
    ownerWith({
      [`POST /api/sms/messages/${failedBirthday.id}/resend`]: () =>
        json(200, { ...failedBirthday, attempts: 2, errorCode: 409 }),
    });

    renderApp("/sms", { session: session() });
    await screen.findByText("سارا محمدی");
    fireEvent.click(
      screen.getByRole("button", { name: "ارسال دوباره پیامک تولد برای سارا محمدی" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "بفرست" }));

    expect(
      await screen.findByText("پیامک تولد برای سارا محمدی باز هم فرستاده نشد: ناموفق."),
    ).toBeInTheDocument();
  });

  it("SmsMessagesPage_ResendRefused_ShowsThePersianReasonUnderTheRow", async () => {
    ownerWith({
      [`POST /api/sms/messages/${failedBirthday.id}/resend`]: () =>
        problem(422, "Notifications.OutsideSendingHours"),
    });

    renderApp("/sms", { session: session() });
    await screen.findByText("سارا محمدی");
    fireEvent.click(
      screen.getByRole("button", { name: "ارسال دوباره پیامک تولد برای سارا محمدی" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "بفرست" }));

    expect(
      await screen.findByText("پیامک فقط بین ساعت ۸:۰۰ تا ۲۲:۰۰ فرستاده می‌شود."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "بفرست" })).toBeEnabled();
  });

  // ---- Who sees it ----

  it("SmsMessagesPage_StaffUser_SeesNoAccessMessage", async () => {
    // BUSINESS_RULES.md §1: every SMS costs money, and the SMS pages are the Owner's.
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/sms", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/sms/messages")).toHaveLength(0);
  });

  it("SmsMessagesPage_Owner_HasAMenuItem", async () => {
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    expect(await screen.findByRole("link", { name: "پیامک‌ها" })).toHaveAttribute("href", "/sms");
  });
});
