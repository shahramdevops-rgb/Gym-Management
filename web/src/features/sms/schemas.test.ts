import type { SmsSettings } from "./api";
import {
  isDebtReminderFilled,
  isKindFilled,
  isValidDebtReminderMinAmount,
  smsSettingsSchema,
  toFormValues,
  toInput,
} from "./schemas";

const off = { enabled: false, threshold: null, sendTime: null };

const settings: SmsSettings = {
  enabled: true,
  subscriptionExpiring: {
    enabled: true,
    threshold: 7,
    sendTime: "09:15:00",
  },
  lowSessions: off,
  birthday: { enabled: false, threshold: 2, sendTime: null },
  payableDue: off,
  ownerPhone: "+989121234567",
  dailyReport: { enabled: true, phone: "+989351234567", sendTime: "22:45:00" },
  subscriptionReceipt: { enabled: true, morningSendTime: "10:00:00" },
  welcome: { enabled: false, morningSendTime: "10:00:00" },
  debtReminder: { enabled: true, daysAfter: 30, sendTime: "11:00:00", minAmount: 200000 },
  absence: { enabled: true, threshold: 14, sendTime: "11:30:00" },
  broadcast: { enabled: true },
  version: 3,
};

describe("sms schemas", () => {
  it("toFormValues_ThenToInput_GivesBackWhatTheApiSent", () => {
    const input = toInput(toFormValues(settings), settings.version);

    expect(input).toEqual({
      ...settings,
      ownerPhone: "0912 123 4567",
      // The time is the server's own; the page only sends the switch and the number.
      dailyReport: { enabled: true, phone: "0935 123 4567" },
      // Only the switch: the morning time is fixed in the code.
      subscriptionReceipt: { enabled: true },
      welcome: { enabled: false },
      // The amount goes back as text, never a float (§13).
      debtReminder: { enabled: true, daysAfter: 30, sendTime: "11:00:00", minAmount: "200000" },
    });
  });

  it("toInput_DebtReminderTypedInPersianAndBlank_BecomesNumbersTextAndNulls", () => {
    const values = toFormValues(settings);
    values.debtReminder = {
      enabled: false,
      daysAfter: "۴۵",
      sendTime: "",
      minAmount: "۱٬۵۰۰٬۰۰۰٫۵",
    };

    expect(toInput(values, 3).debtReminder).toEqual({
      enabled: false,
      daysAfter: 45,
      sendTime: null,
      minAmount: "1500000.5",
    });

    values.debtReminder = { enabled: false, daysAfter: "", sendTime: "", minAmount: "" };
    expect(toInput(values, 3).debtReminder).toEqual({
      enabled: false,
      daysAfter: null,
      sendTime: null,
      minAmount: null,
    });
  });

  it.each([
    ["1000", true],
    ["۱٬۰۰۰", true],
    ["1000.25", true],
    ["9999999999999999.99", true],
    ["999", false],
    ["0", false],
    ["1000.255", false],
    ["99999999999999999", false],
    ["هزار", false],
  ])("isValidDebtReminderMinAmount_%s_Is%s", (text, valid) => {
    expect(isValidDebtReminderMinAmount(text)).toBe(valid);
  });

  it("isDebtReminderFilled_WithoutTheAmount_IsNotFilled", () => {
    const debt = { enabled: false, daysAfter: "30", sendTime: "11:00", minAmount: "" };

    expect(isDebtReminderFilled(debt)).toBe(false);
    expect(isDebtReminderFilled({ ...debt, minAmount: "200000" })).toBe(true);
  });

  it("toInput_PersianDigitsAndBlankFields_BecomeNumbersAndNulls", () => {
    const values = toFormValues({ ...settings, ownerPhone: null });
    values.birthday = { enabled: false, threshold: "۳", sendTime: "" };

    const input = toInput(values, 3);

    expect(input.birthday).toEqual({
      enabled: false,
      threshold: 3,
      sendTime: null,
    });
    expect(input.ownerPhone).toBeNull();
  });

  it("isKindFilled_ChequesWithoutTheOwnersNumber_IsNotFilled", () => {
    const kind = {
      enabled: false,
      threshold: "3",
      sendTime: "10:00",
    };

    expect(isKindFilled("payableDue", kind, "")).toBe(false);
    expect(isKindFilled("payableDue", kind, "09121234567")).toBe(true);
    expect(isKindFilled("birthday", kind, "")).toBe(true);
  });

  it.each([
    ["subscriptionExpiring", "0", false],
    ["subscriptionExpiring", "30", true],
    ["lowSessions", "11", false],
    ["birthday", "0", false],
    ["birthday", "1", true],
    ["birthday", "8", false],
    ["payableDue", "31", false],
    ["absence", "6", false],
    ["absence", "7", true],
    ["absence", "۶۰", true],
    ["absence", "61", false],
  ] as const)("smsSettingsSchema_%s_%s_Valid_%s", (kind, threshold, valid) => {
    const values = toFormValues({ ...settings, enabled: false });
    values[kind] = { enabled: false, threshold, sendTime: "" };

    expect(smsSettingsSchema.safeParse(values).success).toBe(valid);
  });

  it("smsSettingsSchema_DailyReportOnWithoutItsNumber_IsRefusedUnderItsSwitch", () => {
    const values = toFormValues(settings);
    values.dailyReport = { enabled: true, phone: " " };

    const result = smsSettingsSchema.safeParse(values);

    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.path.join("."))).toEqual([
      "dailyReport.enabled",
    ]);
  });

  it("toInput_DailyReportNumberBlank_IsNull", () => {
    const values = toFormValues(settings);
    values.dailyReport = { enabled: false, phone: "" };

    expect(toInput(values, 3).dailyReport).toEqual({ enabled: false, phone: null });
  });
});
