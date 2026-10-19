import type { SmsSettings } from "./api";
import { isKindFilled, smsSettingsSchema, toFormValues, toInput } from "./schemas";

const off = { enabled: false, threshold: null, sendTime: null };

const settings: SmsSettings = {
  enabled: true,
  subscriptionExpiring: {
    enabled: true,
    threshold: 7,
    sendTime: "09:15:00",
  },
  lowSessions: off,
  birthday: { enabled: false, threshold: 0, sendTime: null },
  payableDue: off,
  ownerPhone: "+989121234567",
  dailyReport: { enabled: true, phone: "+989351234567", sendTime: "22:45:00" },
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
    });
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
    ["birthday", "0", true],
    ["birthday", "8", false],
    ["payableDue", "31", false],
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
