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

import type { SmsCredit, SmsKindSettings, SmsSettings } from "../api";

const off: SmsKindSettings = {
  enabled: false,
  threshold: null,
  sendTime: null,
  templateName: null,
};

/** What a fresh database holds until the Owner fills the page (BUSINESS_RULES.md §10). */
const empty: SmsSettings = {
  enabled: false,
  subscriptionExpiring: off,
  lowSessions: off,
  birthday: off,
  payableDue: off,
  ownerPhone: null,
  version: 1,
};

const filled: SmsSettings = {
  enabled: true,
  subscriptionExpiring: {
    enabled: true,
    threshold: 7,
    sendTime: "09:15:00",
    templateName: "gymExpiring",
  },
  lowSessions: off,
  birthday: { enabled: false, threshold: 0, sendTime: null, templateName: null },
  payableDue: { enabled: true, threshold: 3, sendTime: "22:00:00", templateName: "gymPayableDue" },
  ownerPhone: "+989121234567",
  version: 5,
};

function ownerWith(settings: SmsSettings, extra: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    ...signedInHandlers(owner),
    "GET /api/sms/settings": () => json(200, settings),
    "GET /api/sms/credit": () => json(200, testMode),
    ...extra,
  });
}

/** `Sms:Provider` is `Fake`: nothing is really sent, so there is no credit (BUSINESS_RULES.md §10). */
const testMode: SmsCredit = { isTestMode: true, remainingToman: null };

function group(name: string) {
  return within(screen.getByRole("group", { name }));
}

function switchOf(name: string) {
  return group(name).getByRole("checkbox", { name: "روشن" });
}

/** Fills one kind's number, hour and template, as the Owner would. */
function fillKind(name: string, threshold: string, hour: string, template: string) {
  const kind = group(name);
  fireEvent.change(kind.getByLabelText(/^(چند روز|تعداد جلسه)/), { target: { value: threshold } });
  fireEvent.change(kind.getByLabelText("ساعت ارسال"), { target: { value: hour } });
  fireEvent.change(kind.getByLabelText("نام قالب در کاوه‌نگار"), { target: { value: template } });
}

describe("SmsSettingsPage", () => {
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
      "GET /api/sms/credit": () => json(200, { isTestMode: false, remainingToman: 125000 }),
    });

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText(/اعتبار باقی‌ماندهٔ پنل پیامک/)).toBeInTheDocument();
    expect(screen.getByText("۱۲۵٬۰۰۰ تومان")).toBeInTheDocument();
  });

  it("SmsSettingsPage_KavenegarNotAnswering_SaysTheCreditIsUnavailable", async () => {
    ownerWith(empty, {
      "GET /api/sms/credit": () => json(200, { isTestMode: false, remainingToman: null }),
    });

    renderApp("/sms-settings", { session: session() });

    expect(await screen.findByText("اعتبار پنل پیامک الان در دسترس نیست.")).toBeInTheDocument();
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
    for (const name of ["پایان اشتراک", "جلسات رو به اتمام", "تولد", "چک و قسط"]) {
      expect(switchOf(name)).not.toBeChecked();
      expect(switchOf(name)).toBeDisabled();
      expect(group(name).getByLabelText("نام قالب در کاوه‌نگار")).toHaveValue("");
    }
    expect(
      screen.getAllByText("برای روشن کردن، اول همهٔ خانه‌های این بخش را پر کنید."),
    ).toHaveLength(4);
  });

  it("SmsSettingsPage_KindFilled_UnlocksItsSwitch", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "تولد" });

    fillKind("تولد", "۲", "10", "gymBirthdayEarly");

    expect(switchOf("تولد")).toBeEnabled();
    expect(switchOf("پایان اشتراک")).toBeDisabled();
  });

  it("SmsSettingsPage_ChequesFilledWithoutTheOwnersNumber_StaysLocked", async () => {
    ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "چک و قسط" });

    fillKind("چک و قسط", "3", "09", "gymPayableDue");
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
    expect(expiring.getByLabelText("نام قالب در کاوه‌نگار")).toHaveValue("gymExpiring");
    expect(group("تولد").getByLabelText(/^چند روز/)).toHaveValue("0");
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
    fillKind("تولد", "۰", "10", " gymBirthday ");
    fireEvent.change(group("تولد").getByLabelText("دقیقه"), { target: { value: "30" } });
    fireEvent.click(switchOf("تولد"));
    fireEvent.change(screen.getByLabelText("شماره موبایل مدیر"), {
      target: { value: "۰۹۱۲ ۱۲۳ ۴۵۶۷" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("تنظیمات پیامک ذخیره شد.")).toBeInTheDocument();
    const saves = api.requestsTo("PUT", "/api/sms/settings");
    expect(saves).toHaveLength(1);
    expect(await saves[0]!.clone().json()).toEqual({
      enabled: true,
      subscriptionExpiring: off,
      lowSessions: off,
      birthday: { enabled: true, threshold: 0, sendTime: "10:30:00", templateName: "gymBirthday" },
      payableDue: off,
      ownerPhone: "0912 123 4567",
      version: empty.version,
    });
  });

  it("SmsSettingsPage_NumberOutOfRangeOrBadTemplate_IsRefusedBeforeAnythingIsSent", async () => {
    const api = ownerWith(empty);
    renderApp("/sms-settings", { session: session() });
    await screen.findByRole("group", { name: "تولد" });

    fireEvent.change(group("تولد").getByLabelText(/^چند روز/), { target: { value: "8" } });
    fireEvent.change(group("جلسات رو به اتمام").getByLabelText("نام قالب در کاوه‌نگار"), {
      target: { value: "gym_low" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText(/تعداد روز باید از ۰ تا ۷ باشد/)).toBeInTheDocument();
    expect(screen.getByText(/نام قالب فقط حروف انگلیسی و عدد دارد/)).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/sms/settings")).toHaveLength(0);
  });

  it("SmsSettingsPage_KindOnWithAFieldEmptied_CanStillBeTurnedOffAndSaysWhatIsMissing", async () => {
    const api = ownerWith(filled);
    renderApp("/sms-settings", { session: session() });
    await waitFor(() => expect(switchOf("پایان اشتراک")).toBeChecked());

    fireEvent.change(group("پایان اشتراک").getByLabelText("نام قالب در کاوه‌نگار"), {
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

    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(
      await screen.findByText(/تنظیمات پیامک هم‌زمان توسط شخص دیگری تغییر کرد/),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "بارگذاری اطلاعات تازه" })).toBeInTheDocument();
  });
});
