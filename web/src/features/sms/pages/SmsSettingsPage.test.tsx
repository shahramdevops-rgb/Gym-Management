import { fireEvent, screen, waitFor, within } from "@testing-library/react";

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

import type { SmsCredit, SmsKindSettings, SmsSample, SmsSamples, SmsSettings } from "../api";

const off: SmsKindSettings = {
  enabled: false,
  threshold: null,
  sendTime: null,
};

/** What a fresh database holds until the Owner fills the page (BUSINESS_RULES.md §10). */
const empty: SmsSettings = {
  enabled: false,
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
  broadcast: { enabled: false },
  version: 1,
};

const filled: SmsSettings = {
  enabled: true,
  subscriptionExpiring: {
    enabled: true,
    threshold: 7,
    sendTime: "09:15:00",
  },
  lowSessions: off,
  birthday: { enabled: false, threshold: 2, sendTime: null },
  payableDue: { enabled: true, threshold: 3, sendTime: "22:00:00" },
  ownerPhone: "+989121234567",
  dailyReport: { enabled: true, phone: "+989351234567", sendTime: "22:45:00" },
  subscriptionReceipt: { enabled: true, morningSendTime: "10:00:00" },
  welcome: { enabled: true, morningSendTime: "10:00:00" },
  debtReminder: { enabled: true, daysAfter: 30, sendTime: "11:00:00", minAmount: 200000 },
  absence: { enabled: true, threshold: 14, sendTime: "11:30:00" },
  broadcast: { enabled: true },
  version: 5,
};

function ownerWith(settings: SmsSettings, extra: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    ...signedInHandlers(owner),
    "GET /api/sms/settings": () => json(200, settings),
    "GET /api/sms/credit": () => json(200, testMode),
    "GET /api/sms/samples": () => json(200, samples),
    ...extra,
  });
}

function sample(text: string, parts = 1): SmsSample {
  return { text, parts };
}

/** What `GET /api/sms/samples` answers: the API's own texts with a sample member (task 10.15). */
const samples: SmsSamples = {
  subscriptionExpiring: sample(
    "علی عزیز، اشتراک شما در باشگاه پاسارگاد ۱۴۰۵/۰۷/۳۱ به پایان می‌رسد.\nلغو11",
  ),
  lowSessions: sample("علی عزیز، فقط ۲ جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.\nلغو11"),
  birthday: sample("علی عزیز، ۰۷/۳۱ روز تولد شماست؛ …\nلغو11", 2),
  payableDue: sample("یادآوری چک: ۱۲٬۵۰۰٬۰۰۰ تومان، سررسید ۱۴۰۵/۰۷/۳۰، به فروشگاه\nلغو11"),
  dailyReport: sample("گزارش باشگاه پاسارگاد ۰۷/۲۸\nورود: ۴۸\nلغو11", 2),
  subscriptionReceipt: sample("علی عزیز، اشتراک ۱۲ جلسه‌ای شما … سپاس!\nلغو11", 2),
  welcome: sample("علی عزیز، به باشگاه پاسارگاد خوش آمدید!\nلغو11"),
  debtReminder: sample("علی عزیز، مانده حساب شما در باشگاه پاسارگاد ۴۵۰٬۰۰۰ تومان است.\nلغو11", 2),
  absence: sample("علی عزیز، جای شما در باشگاه پاسارگاد خالی است!\nلغو11"),
};

/** The phone-like preview inside one kind's card. */
function previewOf(name: string) {
  return group(name).getByRole("figure", { name: "پیش‌نمایش پیامک" });
}

/** `Sms:Provider` is `Fake`: nothing is really sent, so there is no credit (BUSINESS_RULES.md §10). */
const testMode: SmsCredit = { isTestMode: true, remainingToman: null, creditUsedUp: false };

function group(name: string) {
  return within(screen.getByRole("group", { name }));
}

function switchOf(name: string) {
  return group(name).getByRole("checkbox", { name: "روشن" });
}

/** Fills one kind's number and hour, as the Owner would. */
function fillKind(name: string, threshold: string, hour: string) {
  const kind = group(name);
  fireEvent.change(kind.getByLabelText(/^(چند روز|تعداد جلسه)/), { target: { value: threshold } });
  fireEvent.change(kind.getByLabelText("ساعت ارسال"), { target: { value: hour } });
}

describe("SmsSettingsPage", () => {
  // ---- The cards and their previews (task 10.15) ----

  it("SmsSettingsPage_EachKind_ShowsTheMessageItSendsAndItsParts", async () => {
    ownerWith(empty);

    renderApp("/sms-settings", { session: session() });

    await waitFor(() =>
      expect(previewOf("خوش‌آمد")).toHaveTextContent("علی عزیز، به باشگاه پاسارگاد خوش آمدید!"),
    );
    expect(previewOf("خوش‌آمد")).toHaveTextContent("۱ بخش · نام و عددها نمونه‌اند");
    expect(previewOf("گزارش روزانه")).toHaveTextContent("گزارش باشگاه پاسارگاد ۰۷/۲۸");
    expect(previewOf("گزارش روزانه")).toHaveTextContent("۲ بخش");
    for (const name of [
      "پایان اشتراک",
      "جلسات رو به اتمام",
      "تولد",
      "چک و قسط",
      "رسید خرید اشتراک",
      "یادآوری بدهی",
      "پیامک غیبت",
    ]) {
      expect(previewOf(name)).toHaveTextContent("لغو11");
    }
  });

  it("SmsSettingsPage_SamplesUnreadable_SaysSoAndTheSettingsStillWork", async () => {
    ownerWith(empty, { "GET /api/sms/samples": () => problem(500, "Server.Error") });

    renderApp("/sms-settings", { session: session() });

    await waitFor(
      () => expect(previewOf("پیامک غیبت")).toHaveTextContent("پیش‌نمایش خوانده نشد."),
      {
        // The query is tried once more before it fails.
        timeout: 3000,
      },
    );
    expect(switchOf("خوش‌آمد")).toBeEnabled();
  });

  it("SmsSettingsPage_Kinds_AreGroupedByWhoReceivesThem", async () => {
    ownerWith(empty);

    renderApp("/sms-settings", { session: session() });

    const members = within(await screen.findByRole("region", { name: "پیامک به اعضا" }));
    expect(members.getByRole("group", { name: "یادآوری بدهی" })).toBeInTheDocument();
    expect(members.queryByRole("group", { name: "چک و قسط" })).not.toBeInTheDocument();
    const toOwner = within(screen.getByRole("region", { name: "پیامک به شما (مدیر)" }));
    expect(toOwner.getByRole("group", { name: "چک و قسط" })).toBeInTheDocument();
    expect(toOwner.getByRole("group", { name: "گزارش روزانه" })).toBeInTheDocument();
  });

  // ---- The credit (task 10.4) ----

  it("SmsSettingsPage_TestMode_SaysNothingIsReallySent", async () => {
    ownerWith(empty);

    renderApp("/sms-settings", { session: session() });

    expect(
      await screen.findByText("حالت آزمایشی: پیامکی واقعاً فرستاده نمی‌شود و فقط ثبت می‌شود."),
    ).toBeInTheDocument();
  });

  it("SmsSettingsPage_KavenegarAnswers_ShowsTheRemainingCreditInToman", async () => {
    ownerWith(empty, {
      "GET /api/sms/credit": () =>
        json(200, { isTestMode: false, remainingToman: 125000, creditUsedUp: false }),
    });

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText(/اعتبار باقی‌ماندهٔ پنل پیامک/)).toBeInTheDocument();
    // The header shows it too (§10 *The credit in the header*); this is the page's own line.
    expect(within(screen.getByRole("main")).getByText("۱۲۵٬۰۰۰ تومان")).toBeInTheDocument();
  });

  it("SmsSettingsPage_KavenegarNotAnswering_SaysTheCreditIsUnavailable", async () => {
    ownerWith(empty, {
      "GET /api/sms/credit": () =>
        json(200, { isTestMode: false, remainingToman: null, creditUsedUp: false }),
    });

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText("اعتبار پنل پیامک الان در دسترس نیست.")).toBeInTheDocument();
  });

  it("SmsSettingsPage_CreditUsedUp_WarnsEvenWhenKavenegarDoesNotAnswer", async () => {
    ownerWith(empty, {
      "GET /api/sms/credit": () =>
        json(200, { isTestMode: false, remainingToman: null, creditUsedUp: true }),
    });

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByRole("alert")).toHaveTextContent(/اعتبار پنل پیامک تمام شده/);
    expect(screen.getByText("اعتبار پنل پیامک الان در دسترس نیست.")).toBeInTheDocument();
  });

  it("SmsSettingsPage_CreditNotUsedUp_NoWarning", async () => {
    ownerWith(empty, {
      "GET /api/sms/credit": () =>
        json(200, { isTestMode: false, remainingToman: 125000, creditUsedUp: false }),
    });

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText("۱۲۵٬۰۰۰ تومان")).toBeInTheDocument();
    expect(screen.queryByText(/اعتبار پنل پیامک تمام شده/)).not.toBeInTheDocument();
  });

  // ---- Who sees it ----

  it("SmsSettingsPage_StaffUser_SeesNoAccessMessage", async () => {
    // BUSINESS_RULES.md §1: SMS settings are the Owner's.
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_Owner_HasAMenuItem", async () => {
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    expect(await screen.findByRole("link", { name: "تنظیمات پیامک" })).toHaveAttribute(
      "href",
      "/sms-settings",
    );
  });

  it("SmsSettingsPage_StaffUser_HasNoMenuItem", async () => {
    mockApi(signedInHandlers(staffUser));

    renderApp("/", { session: session() });

    await screen.findByText(staffUser.fullName);
    expect(screen.queryByRole("link", { name: "تنظیمات پیامک" })).not.toBeInTheDocument();
  });

  // ---- Empty and off ----

  it("SmsSettingsPage_FreshSettings_ShowEverythingOffAndEmptyWithTheSwitchesLocked", async () => {
    ownerWith(empty);

    renderApp("/sms-settings", { session: session() });

    expect(
      await screen.findByRole("checkbox", { name: "ارسال پیامک روشن باشد" }),
    ).not.toBeChecked();
    for (const name of [
      "پایان اشتراک",
      "جلسات رو به اتمام",
      "تولد",
      "چک و قسط",
      "یادآوری بدهی",
      "پیامک غیبت",
    ]) {
      expect(switchOf(name)).not.toBeChecked();
      expect(switchOf(name)).toBeDisabled();
      expect(group(name).getByLabelText("ساعت ارسال")).toHaveValue("");
    }
    expect(
      screen.getAllByText("برای روشن کردن، اول همهٔ خانه‌های این بخش را پر کنید."),
    ).toHaveLength(6);
  });

  it("SmsSettingsPage_KindFilled_UnlocksItsSwitch", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "تولد" });

    fillKind("تولد", "۲", "10");

    expect(switchOf("تولد")).toBeEnabled();
    expect(switchOf("پایان اشتراک")).toBeDisabled();
  });

  it("SmsSettingsPage_ChequesFilledWithoutTheOwnersNumber_StaysLocked", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "چک و قسط" });

    fillKind("چک و قسط", "3", "09");
    expect(switchOf("چک و قسط")).toBeDisabled();

    fireEvent.change(screen.getByLabelText("شماره موبایل مدیر"), {
      target: { value: "09121234567" },
    });
    expect(switchOf("چک و قسط")).toBeEnabled();
  });

  // ---- The send time ----

  it("SmsSettingsPage_ChoosingAnHour_FillsInTheQuarterAndTwentyTwoAllowsOnlyZero", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    const birthday = within(await screen.findByRole("group", { name: "تولد" }));
    const minute = birthday.getByLabelText("دقیقه");
    expect(minute).toBeDisabled();

    fireEvent.change(birthday.getByLabelText("ساعت ارسال"), { target: { value: "10" } });
    expect(minute).toBeEnabled();
    expect(minute).toHaveValue("00");
    expect(
      within(minute)
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["۰۰", "۱۵", "۳۰", "۴۵"]);

    fireEvent.change(minute, { target: { value: "45" } });
    fireEvent.change(birthday.getByLabelText("ساعت ارسال"), { target: { value: "22" } });
    expect(minute).toHaveValue("00");
    expect(within(minute).getAllByRole("option")).toHaveLength(1);
  });

  it("SmsSettingsPage_HoursRunFromEightToTwentyTwo", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    const hour = within(await screen.findByRole("group", { name: "تولد" })).getByLabelText(
      "ساعت ارسال",
    );

    const options = within(hour)
      .getAllByRole("option")
      .map((option) => option.textContent);

    expect(options[1]).toBe("۰۸");
    expect(options.at(-1)).toBe("۲۲");
    expect(options).toHaveLength(16);
  });

  // ---- Loaded settings ----

  it("SmsSettingsPage_SavedSettings_FillTheForm", async () => {
    ownerWith(filled);

    renderApp("/sms-settings", { session: session() });

    await waitFor(() => expect(switchOf("پایان اشتراک")).toBeChecked());
    expect(screen.getByRole("checkbox", { name: "ارسال پیامک روشن باشد" })).toBeChecked();
    const expiring = group("پایان اشتراک");
    expect(expiring.getByLabelText(/^چند روز/)).toHaveValue("7");
    expect(expiring.getByLabelText("ساعت ارسال")).toHaveValue("09");
    expect(expiring.getByLabelText("دقیقه")).toHaveValue("15");
    expect(group("تولد").getByLabelText(/^چند روز/)).toHaveValue("2");
    expect(screen.getByLabelText("شماره موبایل مدیر")).toHaveValue("۰۹۱۲ ۱۲۳ ۴۵۶۷");
  });

  // ---- Saving ----

  it("SmsSettingsPage_Save_SendsEveryKindWithTheVersionAndSaysSaved", async () => {
    const api = ownerWith(empty, {
      "PUT /api/sms/settings": () => json(200, { ...filled, version: 2 }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "تولد" });

    fireEvent.click(screen.getByRole("checkbox", { name: "ارسال پیامک روشن باشد" }));
    fillKind("تولد", "۱", "10");
    fireEvent.change(group("تولد").getByLabelText("دقیقه"), { target: { value: "30" } });
    fireEvent.click(switchOf("تولد"));
    fireEvent.change(screen.getByLabelText("شماره موبایل مدیر"), {
      target: { value: "۰۹۱۲ ۱۲۳ ۴۵۶۷" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("تغییرها ثبت شد.")).toBeInTheDocument();
    expect(saveRow().getByText("تغییرها ثبت شد.")).toBeInTheDocument();
    const saves = api.requestsTo("PUT", "/api/sms/settings");
    expect(saves).toHaveLength(1);
    expect(await saves[0]!.clone().json()).toEqual({
      enabled: true,
      subscriptionExpiring: off,
      lowSessions: off,
      birthday: { enabled: true, threshold: 1, sendTime: "10:30:00" },
      payableDue: off,
      ownerPhone: "0912 123 4567",
      dailyReport: { enabled: false, phone: null },
      subscriptionReceipt: { enabled: false },
      welcome: { enabled: false },
      debtReminder: { enabled: false, daysAfter: null, sendTime: null, minAmount: null },
      absence: off,
      broadcast: { enabled: false },
      version: empty.version,
    });
  });

  it("SmsSettingsPage_NumberOutOfRange_IsRefusedBeforeAnythingIsSent", async () => {
    const api = ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "تولد" });

    fireEvent.change(group("تولد").getByLabelText(/^چند روز/), { target: { value: "8" } });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText(/تعداد روز باید از ۱ تا ۷ باشد/)).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_KindOnWithAFieldEmptied_CanStillBeTurnedOffAndSaysWhatIsMissing", async () => {
    const api = ownerWith(filled);
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("پایان اشتراک")).toBeChecked());

    fireEvent.change(group("پایان اشتراک").getByLabelText(/^چند روز/), {
      target: { value: "" },
    });
    expect(switchOf("پایان اشتراک")).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await group("پایان اشتراک").findByText(
        "برای روشن کردن این پیامک، همهٔ خانه‌هایش را پر کنید.",
      ),
    ).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_ServerRefusesTheNumber_ShowsItUnderTheNumber", async () => {
    ownerWith(empty, {
      "PUT /api/sms/settings": () => problem(400, "Members.PhoneNotMobile"),
    });
    renderApp("/sms-settings", { session: session() });

    fireEvent.change(await screen.findByLabelText("شماره موبایل مدیر"), {
      target: { value: "02112345678" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await screen.findByText("شماره باید موبایل باشد؛ شمارهٔ ثابت پذیرفته نمی‌شود."),
    ).toBeInTheDocument();
  });

  // ---- The daily report (task 10.7) ----

  it("SmsSettingsPage_DailyReport_SaysItsFixedTimeAndStaysLockedUntilItsNumber", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "گزارش روزانه" });

    expect(group("گزارش روزانه").getByText(/هر شب ساعت ۲۲:۴۵ برای همان روز/)).toBeInTheDocument();
    expect(group("گزارش روزانه").queryByLabelText("ساعت ارسال")).not.toBeInTheDocument();
    expect(switchOf("گزارش روزانه")).toBeDisabled();

    // The cheques' number does not unlock it: the report has its own.
    fireEvent.change(screen.getByLabelText("شماره موبایل مدیر"), {
      target: { value: "09121234567" },
    });
    expect(switchOf("گزارش روزانه")).toBeDisabled();

    fireEvent.change(screen.getByLabelText("شماره موبایل گیرندهٔ گزارش"), {
      target: { value: "09351234567" },
    });
    expect(switchOf("گزارش روزانه")).toBeEnabled();
  });

  it("SmsSettingsPage_DailyReportSaved_FillsItsSwitchAndNumber", async () => {
    ownerWith(filled);

    renderApp("/sms-settings", { session: session() });

    await waitFor(() => expect(switchOf("گزارش روزانه")).toBeChecked());
    expect(screen.getByLabelText("شماره موبایل گیرندهٔ گزارش")).toHaveValue("۰۹۳۵ ۱۲۳ ۴۵۶۷");
  });

  it("SmsSettingsPage_DailyReportTurnedOn_SendsItsSwitchAndNumber", async () => {
    const api = ownerWith(empty, {
      "PUT /api/sms/settings": () => json(200, { ...filled, version: 2 }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "گزارش روزانه" });

    fireEvent.change(screen.getByLabelText("شماره موبایل گیرندهٔ گزارش"), {
      target: { value: "۰۹۳۵ ۱۲۳ ۴۵۶۷" },
    });
    fireEvent.click(switchOf("گزارش روزانه"));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("تغییرها ثبت شد.")).toBeInTheDocument();
    const body = await api.requestsTo("PUT", "/api/sms/settings")[0]!.clone().json();
    expect(body.dailyReport).toEqual({ enabled: true, phone: "0935 123 4567" });
  });

  it("SmsSettingsPage_SubscriptionReceipt_IsOnlyASwitchAndSaysWhenItGoes", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });

    const receipt = await screen.findByRole("group", { name: "رسید خرید اشتراک" });
    // Nothing to fill in, so it can be turned on straight away.
    expect(switchOf("رسید خرید اشتراک")).toBeEnabled();
    expect(switchOf("رسید خرید اشتراک")).not.toBeChecked();
    expect(within(receipt).queryAllByRole("textbox")).toHaveLength(0);
    expect(within(receipt).getByText(/صبح روز بعد ساعت ۱۰:۰۰/)).toBeInTheDocument();
  });

  it("SmsSettingsPage_SubscriptionReceiptSaved_ShowsItOn", async () => {
    ownerWith(filled);
    renderApp("/sms-settings", { session: session() });

    await waitFor(() => expect(switchOf("رسید خرید اشتراک")).toBeChecked());
  });

  it("SmsSettingsPage_SubscriptionReceiptTurnedOn_SendsItsSwitch", async () => {
    const api = ownerWith(empty, {
      "PUT /api/sms/settings": () => json(200, { ...filled, version: 2 }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "رسید خرید اشتراک" });

    fireEvent.click(switchOf("رسید خرید اشتراک"));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("تغییرها ثبت شد.")).toBeInTheDocument();
    const body = await api.requestsTo("PUT", "/api/sms/settings")[0]!.clone().json();
    expect(body.subscriptionReceipt).toEqual({ enabled: true });
  });

  it("SmsSettingsPage_Welcome_IsOnlyASwitchAndSaysWhenItGoes", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });

    const welcome = await screen.findByRole("group", { name: "خوش‌آمد" });
    expect(switchOf("خوش‌آمد")).toBeEnabled();
    expect(switchOf("خوش‌آمد")).not.toBeChecked();
    expect(within(welcome).queryAllByRole("textbox")).toHaveLength(0);
    expect(within(welcome).getByText(/اولین پلن/)).toBeInTheDocument();
    expect(within(welcome).getByText(/صبح روز بعد ساعت ۱۰:۰۰/)).toBeInTheDocument();
  });

  it("SmsSettingsPage_WelcomeSaved_ShowsItOn", async () => {
    ownerWith(filled);
    renderApp("/sms-settings", { session: session() });

    await waitFor(() => expect(switchOf("خوش‌آمد")).toBeChecked());
  });

  it("SmsSettingsPage_WelcomeTurnedOn_SendsItsSwitch", async () => {
    const api = ownerWith(empty, {
      "PUT /api/sms/settings": () => json(200, { ...filled, version: 2 }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "خوش‌آمد" });

    fireEvent.click(switchOf("خوش‌آمد"));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("تغییرها ثبت شد.")).toBeInTheDocument();
    const body = await api.requestsTo("PUT", "/api/sms/settings")[0]!.clone().json();
    expect(body.welcome).toEqual({ enabled: true });
  });

  it("SmsSettingsPage_DebtReminderEmpty_CannotBeTurnedOnUntilEveryFieldIsFilled", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "یادآوری بدهی" });

    expect(switchOf("یادآوری بدهی")).toBeDisabled();

    fireEvent.change(group("یادآوری بدهی").getByLabelText(/^چند روز/), { target: { value: "۳۰" } });
    fireEvent.change(group("یادآوری بدهی").getByLabelText("ساعت ارسال"), {
      target: { value: "11" },
    });
    expect(switchOf("یادآوری بدهی")).toBeDisabled();

    fireEvent.change(group("یادآوری بدهی").getByLabelText(/^حداقل کل بدهی/), {
      target: { value: "۲۰۰۰۰۰" },
    });
    expect(switchOf("یادآوری بدهی")).toBeEnabled();
  });

  it("SmsSettingsPage_DebtReminderFilledAndOn_SendsDaysTimeAndAmountAsText", async () => {
    const api = ownerWith(empty, {
      "PUT /api/sms/settings": () => json(200, { ...filled, version: 2 }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "یادآوری بدهی" });

    const debt = group("یادآوری بدهی");
    fireEvent.change(debt.getByLabelText(/^چند روز/), { target: { value: "۳۰" } });
    fireEvent.change(debt.getByLabelText("ساعت ارسال"), { target: { value: "11" } });
    fireEvent.change(debt.getByLabelText(/^حداقل کل بدهی/), { target: { value: "۲۰۰٬۰۰۰" } });
    fireEvent.click(switchOf("یادآوری بدهی"));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("تغییرها ثبت شد.")).toBeInTheDocument();
    const body = await api.requestsTo("PUT", "/api/sms/settings")[0]!.clone().json();
    expect(body.debtReminder).toEqual({
      enabled: true,
      daysAfter: 30,
      sendTime: "11:00:00",
      minAmount: "200000",
    });
  });

  // ---- The absence SMS (task 10.12) ----

  it("SmsSettingsPage_AbsenceFilledAndOn_SendsItsDaysAndTime", async () => {
    const api = ownerWith(empty, {
      "PUT /api/sms/settings": () => json(200, { ...filled, version: 2 }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "پیامک غیبت" });

    expect(switchOf("پیامک غیبت")).toBeDisabled();
    fillKind("پیامک غیبت", "۱۴", "11");
    expect(switchOf("پیامک غیبت")).toBeEnabled();
    fireEvent.click(switchOf("پیامک غیبت"));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("تغییرها ثبت شد.")).toBeInTheDocument();
    const body = await api.requestsTo("PUT", "/api/sms/settings")[0]!.clone().json();
    expect(body.absence).toEqual({ enabled: true, threshold: 14, sendTime: "11:00:00" });
  });

  it("SmsSettingsPage_AbsenceDaysOutOfRange_IsRefusedBeforeAnythingIsSent", async () => {
    const api = ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "پیامک غیبت" });

    fireEvent.change(group("پیامک غیبت").getByLabelText(/^چند روز/), { target: { value: "6" } });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await group("پیامک غیبت").findByText("تعداد روز باید از ۷ تا ۶۰ باشد."),
    ).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_AbsenceSaved_ShowsItOnWithItsDays", async () => {
    ownerWith(filled);
    renderApp("/sms-settings", { session: session() });

    await waitFor(() => expect(switchOf("پیامک غیبت")).toBeChecked());
    expect(group("پیامک غیبت").getByLabelText(/^چند روز/)).toHaveValue("14");
  });

  it("SmsSettingsPage_DebtReminderSaved_ShowsItOnWithItsAmount", async () => {
    ownerWith(filled);
    renderApp("/sms-settings", { session: session() });

    await waitFor(() => expect(switchOf("یادآوری بدهی")).toBeChecked());
    expect(group("یادآوری بدهی").getByLabelText(/^چند روز/)).toHaveValue("30");
    expect(group("یادآوری بدهی").getByLabelText(/^حداقل کل بدهی/)).toHaveValue("۲۰۰٬۰۰۰");
  });

  it("SmsSettingsPage_DebtReminderAmountBelowTheFloorOrDaysOutOfRange_IsRefusedBeforeAnythingIsSent", async () => {
    const api = ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "یادآوری بدهی" });

    fireEvent.change(group("یادآوری بدهی").getByLabelText(/^چند روز/), { target: { value: "91" } });
    fireEvent.change(group("یادآوری بدهی").getByLabelText(/^حداقل کل بدهی/), {
      target: { value: "999" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await group("یادآوری بدهی").findByText("تعداد روز باید از ۱ تا ۹۰ باشد."),
    ).toBeInTheDocument();
    expect(group("یادآوری بدهی").getByText(/حداقل مبلغ باید ۱٬۰۰۰ تومان/)).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_ServerRefusesTheMinimumAmount_ShowsItUnderTheAmount", async () => {
    ownerWith(filled, {
      "PUT /api/sms/settings": () => problem(400, "Sms.DebtReminderMinAmountOutOfRange"),
    });
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("یادآوری بدهی")).toBeChecked());

    fireEvent.change(group("یادآوری بدهی").getByLabelText(/^حداقل کل بدهی/), {
      target: { value: "۳۰۰۰۰۰" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await group("یادآوری بدهی").findByText(/حداقل مبلغ باید ۱٬۰۰۰ تومان/),
    ).toBeInTheDocument();
  });

  it("SmsSettingsPage_DailyReportOnWithItsNumberEmptied_SaysWhatIsMissing", async () => {
    const api = ownerWith(filled);
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("گزارش روزانه")).toBeChecked());

    fireEvent.change(screen.getByLabelText("شماره موبایل گیرندهٔ گزارش"), {
      target: { value: "" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await group("گزارش روزانه").findByText(
        "برای روشن کردن این پیامک، همهٔ خانه‌هایش را پر کنید.",
      ),
    ).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_ServerRefusesTheReportsNumber_ShowsItUnderThatNumber", async () => {
    ownerWith(empty, {
      "PUT /api/sms/settings": () => problem(400, "Sms.DailyReportPhoneInvalid"),
    });
    renderApp("/sms-settings", { session: session() });

    fireEvent.change(await screen.findByLabelText("شماره موبایل گیرندهٔ گزارش"), {
      target: { value: "02112345678" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await group("گزارش روزانه").findByText(
        "شمارهٔ گزارش روزانه باید یک شمارهٔ موبایل ایران باشد.",
      ),
    ).toBeInTheDocument();
  });

  it("SmsSettingsPage_ServerFieldError_IsShownUnderItsField", async () => {
    ownerWith(empty, {
      "PUT /api/sms/settings": () =>
        problem(400, "Validation", {
          errors: {
            "lowSessions.sendTime": [{ code: "Sms.SendTimeOutOfRange", description: "x" }],
          },
        }),
    });
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "جلسات رو به اتمام" });

    fireEvent.click(screen.getByRole("checkbox", { name: "ارسال پیامک روشن باشد" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await group("جلسات رو به اتمام").findByText(/ساعت ارسال باید بین ۸:۰۰ و ۲۲:۰۰/),
    ).toBeInTheDocument();
  });

  it("SmsSettingsPage_SomeoneSavedMeanwhile_SaysSoAndOffersToReload", async () => {
    ownerWith(filled, {
      "PUT /api/sms/settings": () => problem(409, "Sms.ChangedConcurrently"),
    });
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("پایان اشتراک")).toBeChecked());

    fireEvent.click(switchOf("پایان اشتراک"));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await saveRow().findByText(/تنظیمات پیامک هم‌زمان توسط شخص دیگری تغییر کرد/),
    ).toBeInTheDocument();
    expect(saveRow().getByRole("button", { name: "بارگذاری اطلاعات تازه" })).toBeInTheDocument();
  });

  it("SmsSettingsPage_ReloadAfterSomeoneSaved_LoadsTheirSettingsWithoutSavingAgain", async () => {
    const api = ownerWith(filled, {
      "PUT /api/sms/settings": () => problem(409, "Sms.ChangedConcurrently"),
    });
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("پایان اشتراک")).toBeChecked());
    fireEvent.click(switchOf("پایان اشتراک"));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));
    const reload = await screen.findByRole("button", { name: "بارگذاری اطلاعات تازه" });

    fireEvent.click(reload);

    await waitFor(() =>
      expect(
        screen.queryByRole("button", { name: "بارگذاری اطلاعات تازه" }),
      ).not.toBeInTheDocument(),
    );
    expect(api.requestsTo("GET", "/api/sms/settings")).toHaveLength(2);
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(1);
  });

  // ---- Saving with nothing changed (asked by the developer, 1405/07/16) ----

  it("SmsSettingsPage_SaveWithNothingChanged_SendsNothingAndSaysSoBesideTheButton", async () => {
    const api = ownerWith(filled);
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("پایان اشتراک")).toBeChecked());

    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await saveRow().findByText("تغییری نداده‌اید؛ چیزی ذخیره نشد.")).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_ChangedAndPutBack_CountsAsNothingChanged", async () => {
    const api = ownerWith(filled);
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("پایان اشتراک")).toBeChecked());

    fireEvent.click(switchOf("پایان اشتراک"));
    fireEvent.click(switchOf("پایان اشتراک"));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await saveRow().findByText("تغییری نداده‌اید؛ چیزی ذخیره نشد.")).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_EditAfterSaving_ClearsTheMessage", async () => {
    ownerWith(filled, {
      "PUT /api/sms/settings": () => json(200, { ...filled, enabled: false, version: 6 }),
    });
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("پایان اشتراک")).toBeChecked());

    fireEvent.click(screen.getByRole("checkbox", { name: "ارسال پیامک روشن باشد" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));
    expect(await screen.findByText("تغییرها ثبت شد.")).toBeInTheDocument();
    expect(saveRow().getByText("تغییرها ثبت شد.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("checkbox", { name: "ارسال پیامک روشن باشد" }));

    await waitFor(() => expect(screen.queryByText("تغییرها ثبت شد.")).not.toBeInTheDocument());
  });
});

/** The row of «ذخیره»: every outcome of a save is shown there, where the Owner is looking. */
function saveRow() {
  return within(screen.getByRole("button", { name: /^(ذخیره|در حال ذخیره…)$/ }).parentElement!);
}
