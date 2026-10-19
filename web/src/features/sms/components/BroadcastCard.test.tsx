import { fireEvent, screen, waitFor, within } from "@testing-library/react";

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

import type {
  Broadcast,
  BroadcastAudiences,
  BroadcastPreview,
  SmsCredit,
  SmsSettings,
} from "../api";

const off = { enabled: false, threshold: null, sendTime: null };

/** All SMS and the broadcast on; every other kind off. */
const settingsOn: SmsSettings = {
  enabled: true,
  subscriptionExpiring: off,
  lowSessions: off,
  birthday: off,
  payableDue: off,
  ownerPhone: null,
  dailyReport: { enabled: false, phone: null, sendTime: "22:45:00" },
  subscriptionReceipt: { enabled: false, morningSendTime: "10:00:00" },
  welcome: { enabled: false, morningSendTime: "10:00:00" },
  debtReminder: { enabled: false, daysAfter: null, sendTime: null, minAmount: null },
  absence: off,
  broadcast: { enabled: true },
  version: 7,
};

const audiences: BroadcastAudiences = {
  activeMembers: { recipients: 120, longestFirstName: "عبدالرحمن" },
  membersWithUsablePlan: { recipients: 45, longestFirstName: "علی" },
};

const preview: BroadcastPreview = {
  recipients: 120,
  longestMessageParts: 1,
  totalParts: 120,
  estimatedCostToman: 16200,
  sampleText: "عبدالرحمن عزیز، فردا تعطیل است.\nلغو11",
};

const credit: SmsCredit = { isTestMode: false, remainingToman: 125000, creditUsedUp: false };

const stopped: Broadcast = {
  id: "01990000-0000-7000-8000-0000000000c1",
  text: "باشگاه فردا تا ساعت ۱۶ تعطیل است.",
  audience: "ActiveMembers",
  status: "Stopped",
  stopReason: "CreditExhausted",
  createdAt: "2026-10-19T06:30:00Z",
  endedAt: "2026-10-19T06:35:00Z",
  sent: 40,
  failed: 1,
  unknown: 0,
  pending: 0,
  costToman: 5400,
};

function broadcastsPage(items: Broadcast[]) {
  return json(200, { items, page: 1, pageSize: 20, totalCount: items.length });
}

function ownerWith(extra: Record<string, Handler> = {}) {
  return mockApi({
    ...signedInHandlers(owner),
    "GET /api/sms/settings": () => json(200, settingsOn),
    "GET /api/sms/credit": () => json(200, credit),
    "GET /api/sms/broadcasts/audiences": () => json(200, audiences),
    "GET /api/sms/broadcasts": () => broadcastsPage([]),
    ...extra,
  });
}

function textBox() {
  return screen.getByLabelText("متن پیامک");
}

function sendButton() {
  return screen.getByRole("button", { name: "بررسی و ارسال" });
}

describe("BroadcastCard", () => {
  // ---- Writing ----

  it("BroadcastCard_Groups_ShowHowManyAreInEach", async () => {
    ownerWith();

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText("(۱۲۰ نفر)")).toBeInTheDocument();
    expect(screen.getByText("(۴۵ نفر)")).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /همه اعضای فعال/ })).toBeChecked();
  });

  it("BroadcastCard_Typing_CountsWithTheLongestFirstNameInTheGroup", async () => {
    ownerWith();
    renderApp("/sms-settings", { session: session() });
    await screen.findByText("(۱۲۰ نفر)");

    fireEvent.change(textBox(), { target: { value: "ب".repeat(60) } });

    // «عبدالرحمن عزیز، » is 16 characters and «\nلغو11» 6: 179 are left of three parts' 201.
    expect(screen.getByText("۶۰ از ۱۷۹ نویسه · ۲ بخش")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("radio", { name: /اعضای دارای پلن/ }));
    expect(screen.getByText("۶۰ از ۱۸۵ نویسه · ۲ بخش")).toBeInTheDocument();
  });

  it("BroadcastCard_TooLongForThreeParts_CannotBeSent", async () => {
    ownerWith();
    renderApp("/sms-settings", { session: session() });
    await screen.findByText("(۱۲۰ نفر)");

    fireEvent.change(textBox(), { target: { value: "ب".repeat(180) } });

    expect(
      screen.getByText("متن بلند است: پیامک هر عضو باید در ۳ بخش جا شود."),
    ).toBeInTheDocument();
    expect(sendButton()).toBeDisabled();
  });

  it("BroadcastCard_BroadcastOff_SaysSoAndCannotBeSent", async () => {
    ownerWith({
      "GET /api/sms/settings": () => json(200, { ...settingsOn, broadcast: { enabled: false } }),
    });
    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText(/پیامک گروهی خاموش است/)).toBeInTheDocument();
    fireEvent.change(textBox(), { target: { value: "فردا تعطیل است." } });
    expect(sendButton()).toBeDisabled();
  });

  // ---- The confirmation ----

  it("BroadcastCard_Review_ShowsTheFiguresAndSendsOnlyAfterConfirming", async () => {
    const api = ownerWith({
      "POST /api/sms/broadcasts/preview": () => json(200, preview),
      "POST /api/sms/broadcasts": () =>
        json(200, { ...stopped, status: "Sending", stopReason: null }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByText("(۱۲۰ نفر)");

    fireEvent.change(textBox(), { target: { value: "  فردا تعطیل است.\r\n" } });
    fireEvent.click(sendButton());

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/به ۱۲۰ نفر/)).toBeInTheDocument();
    expect(within(dialog).getByText("حدود ۱۶٬۲۰۰ تومان")).toBeInTheDocument();
    expect(within(dialog).getByLabelText("نمونهٔ پیامک")).toHaveTextContent(
      "عبدالرحمن عزیز، فردا تعطیل است.",
    );
    expect(await api.requestsTo("POST", "/api/sms/broadcasts/preview")[0]!.clone().json()).toEqual({
      text: "فردا تعطیل است.",
      audience: "ActiveMembers",
    });
    expect(api.requestsTo("POST", "/api/sms/broadcasts")).toHaveLength(0);

    fireEvent.click(within(dialog).getByRole("button", { name: "بله، بفرست" }));

    expect(await screen.findByText(/پیامک گروهی در حال ارسال است/)).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/sms/broadcasts")).toHaveLength(1);
    expect(textBox()).toHaveValue("");
  });

  it("BroadcastCard_SavedButNotQueued_SaysItDidNotStartAndToContinueIt", async () => {
    ownerWith({
      "POST /api/sms/broadcasts/preview": () => json(200, preview),
      "POST /api/sms/broadcasts": () => json(200, { ...stopped, stopReason: "Error" }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByText("(۱۲۰ نفر)");

    fireEvent.change(textBox(), { target: { value: "فردا تعطیل است." } });
    fireEvent.click(sendButton());
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "بله، بفرست" }),
    );

    expect(await screen.findByText(/ارسالش شروع نشد \(خطای پیش‌بینی‌نشده\)/)).toBeInTheDocument();
    expect(screen.queryByText(/در حال ارسال است/)).not.toBeInTheDocument();
  });

  it("BroadcastCard_NothingChargedYet_SaysTheCostCannotBeEstimated", async () => {
    ownerWith({
      "POST /api/sms/broadcasts/preview": () => json(200, { ...preview, estimatedCostToman: null }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByText("(۱۲۰ نفر)");

    fireEvent.change(textBox(), { target: { value: "فردا تعطیل است." } });
    fireEvent.click(sendButton());

    expect(await screen.findByText(/برآورد نمی‌شود/)).toBeInTheDocument();
  });

  it("BroadcastCard_SendRefused_ShowsWhyInTheConfirmation", async () => {
    ownerWith({
      "POST /api/sms/broadcasts/preview": () => json(200, preview),
      "POST /api/sms/broadcasts": () => problem(422, "Notifications.OutsideSendingHours"),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByText("(۱۲۰ نفر)");

    fireEvent.change(textBox(), { target: { value: "فردا تعطیل است." } });
    fireEvent.click(sendButton());
    fireEvent.click(await screen.findByRole("button", { name: "بله، بفرست" }));

    expect(
      await screen.findByText("پیامک فقط بین ساعت ۸:۰۰ تا ۲۲:۰۰ فرستاده می‌شود."),
    ).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  // ---- The list ----

  it("BroadcastCard_StoppedBroadcast_ShowsWhyAndCanBeContinued", async () => {
    const api = ownerWith({
      "GET /api/sms/broadcasts": () => broadcastsPage([stopped]),
      "POST /api/sms/broadcasts/01990000-0000-7000-8000-0000000000c1/resume": () =>
        json(200, { ...stopped, status: "Sending", stopReason: null }),
    });
    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText("متوقف شد")).toBeInTheDocument();
    expect(screen.getByText("اعتبار پنل تمام شد")).toBeInTheDocument();
    expect(screen.getByText(/فرستاده: ۴۰ · ناموفق: ۱/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "پیامک‌های این ارسال" })).toHaveAttribute(
      "href",
      "/sms?broadcast=01990000-0000-7000-8000-0000000000c1",
    );

    fireEvent.click(screen.getByRole("button", { name: "ادامه ارسال" }));
    expect(api.requestsTo("POST", `/api/sms/broadcasts/${stopped.id}/resume`)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "بله، ادامه بده" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/sms/broadcasts/${stopped.id}/resume`)).toHaveLength(1),
    );
  });

  it("BroadcastCard_LatestFinished_ShowsNoListOrStatus", async () => {
    const api = ownerWith({
      "GET /api/sms/broadcasts": () =>
        broadcastsPage([{ ...stopped, status: "Finished", stopReason: null }]),
    });
    renderApp("/sms-settings", { session: session() });

    await waitFor(() => expect(api.requestsTo("GET", "/api/sms/broadcasts")).toHaveLength(1));
    // The sent broadcasts are on «پیامک‌ها»; the card keeps only one that may still need the Owner.
    expect(screen.queryByText("فرستاده شد")).not.toBeInTheDocument();
    expect(screen.queryByText("پیامک‌های گروهی فرستاده‌شده")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "ادامه ارسال" })).not.toBeInTheDocument();
  });

  // ---- Who may open it ----

  it("BroadcastCard_StaffUser_SeesNoAccessMessage", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/sms/broadcasts")).toHaveLength(0);
  });

  it("BroadcastCard_IsOneCardOfSmsSettingsWithNoPageOrMenuItemOfItsOwn", async () => {
    ownerWith();
    renderApp("/sms-settings", { session: session() });

    const card = within(await screen.findByRole("group", { name: "پیامک گروهی" }));
    expect(screen.getByRole("group", { name: "پیامک گروهی" })).toHaveAttribute("id", "broadcast");
    expect(card.getByRole("checkbox", { name: "روشن" })).toBeInTheDocument();
    expect(card.getByLabelText("متن پیامک")).toBeInTheDocument();
    expect(card.getByRole("button", { name: "بررسی و ارسال" })).toHaveAttribute("type", "button");
    const menu = within(screen.getByRole("navigation", { name: "منوی اصلی" }));
    expect(menu.queryByRole("link", { name: /پیامک گروهی/ })).not.toBeInTheDocument();
  });

  it("BroadcastCard_Typing_ShowsTheMessageWithTheLongestFirstName", async () => {
    ownerWith();
    renderApp("/sms-settings", { session: session() });
    const card = within(await screen.findByRole("group", { name: "پیامک گروهی" }));

    fireEvent.change(card.getByLabelText("متن پیامک"), { target: { value: "فردا تعطیل است." } });

    expect(card.getByRole("figure", { name: "پیش‌نمایش پیامک" })).toHaveTextContent(
      "عبدالرحمن عزیز، فردا تعطیل است. لغو11",
    );
  });
});
