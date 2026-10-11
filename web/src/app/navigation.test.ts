import { navigation, sectionFor } from "./navigation";
import { paths } from "./paths";

describe("sectionFor", () => {
  it.each([
    [paths.home, "پذیرش", "ورود با کمد"],
    [paths.attendance, "پذیرش", "داخل باشگاه"],
    [paths.history, "پذیرش", "تاریخچه"],
    [paths.members, "پذیرش", "اعضا"],
    [paths.cafe, "فروش بوفه", "بوفه"],
    [paths.cafeOrders, "فروش بوفه", "سفارش‌های بوفه"],
    [paths.cafeMenu, "فروش بوفه", "منوی بوفه"],
    [paths.dashboard, "مالی", "داشبورد"],
    [paths.expenses, "مالی", "هزینه‌ها"],
    [paths.payables, "مالی", "چک و قسط"],
    [paths.staff, "مدیریت", "کارمندان"],
    [paths.settings, "مدیریت", "تنظیمات"],
    [paths.smsMessages, "مدیریت", "پیامک‌ها"],
    [paths.smsSettings, "مدیریت", "تنظیمات پیامک"],
    [paths.auditLogs, "مدیریت", "گزارش تغییرات"],
    [paths.status, "حساب و سیستم", "وضعیت سیستم"],
    [paths.changePassword, "حساب و سیستم", "تغییر رمز عبور"],
  ])("sectionFor_MenuPath_%s_IsItsOwnItem", (path, group, item) => {
    const section = sectionFor(path);

    expect(section?.group.title).toBe(group);
    expect(section?.item.label).toBe(item);
  });

  it.each([
    paths.newMember,
    paths.member("0199a000-0000-7000-8000-0000000000aa"),
    paths.editMember("0199a000-0000-7000-8000-0000000000aa"),
  ])("sectionFor_MemberPage_%s_IsMembers", (path) => {
    expect(sectionFor(path)?.item.label).toBe("اعضا");
  });

  it("sectionFor_PathUnderAnother_TakesTheLongestMatch", () => {
    // "/cafe" is a prefix of "/cafe/orders"; the orders page is not «بوفه».
    expect(sectionFor("/cafe/orders")?.item.label).toBe("سفارش‌های بوفه");
  });

  it("sectionFor_PathSharingOnlyLetters_DoesNotMatch", () => {
    // "/sms" is the start of "/sms-settings" but not a whole segment of it.
    expect(sectionFor("/sms-settings")?.item.label).toBe("تنظیمات پیامک");
    expect(sectionFor("/smsx")).toBeUndefined();
  });

  it("sectionFor_PathOutsideTheMenu_IsUndefined", () => {
    // "/" is the locker map only, not every page.
    expect(sectionFor(paths.login)).toBeUndefined();
    expect(sectionFor("/no-such-page")).toBeUndefined();
  });

  it("navigation_Tones_AreNeverTheColoursThatMeanSomething", () => {
    // Green and red mean paid and owed, amber means attention, on every screen (roadmap 13.2e).
    const tones = navigation.map((group) => group.tone);

    expect(tones).not.toContain("green");
    expect(tones).not.toContain("red");
    expect(tones).not.toContain("amber");
  });
});
