import { emptyValue, formatDate, formatDateTime } from "@/lib/format";

import { entityLabel, formatAuditValue, shownChanges } from "./labels";

const userNames = new Map([["01990000-0000-7000-8000-0000000000c1", "مدیر باشگاه"]]);

describe("audit labels", () => {
  it("entityLabel_KnownKind_IsPersianAndAnUnknownOneKeepsItsName", () => {
    expect(entityLabel("Subscription")).toBe("اشتراک");
    expect(entityLabel("SomethingNew")).toBe("SomethingNew");
  });

  it("formatAuditValue_Money_IsInToman", () => {
    expect(formatAuditValue(180000, "money", userNames)).toBe("۱۸۰٬۰۰۰ تومان");
  });

  it("formatAuditValue_Rial_IsDividedIntoToman", () => {
    expect(formatAuditValue(1350, "rial", userNames)).toBe("۱۳۵ تومان");
  });

  it("formatAuditValue_UserId_IsTheUsersNameOrAShortCode", () => {
    expect(formatAuditValue("01990000-0000-7000-8000-0000000000c1", "user", userNames)).toBe(
      "مدیر باشگاه",
    );
    expect(formatAuditValue("01a0e43e-7cec-7ab1-9bbf-31a3882ce734", "user", userNames)).toBe(
      "01a0e43e",
    );
  });

  it("formatAuditValue_Kinds_ArePersian", () => {
    expect(formatAuditValue("Card", "paymentMethod", userNames)).toBe("کارت");
    expect(formatAuditValue("Refund", "paymentKind", userNames)).toBe("بازپرداخت");
    expect(formatAuditValue("Cardio", "serviceChargeKind", userNames)).toBe("هوازی");
    expect(formatAuditValue("Logout", "revocationReason", userNames)).toBe("خروج");
  });

  it("formatAuditValue_ByShape_DatesAreJalaliAndTheRestPersian", () => {
    expect(formatAuditValue("2026-10-07", undefined, userNames)).toBe(formatDate("2026-10-07"));
    expect(formatAuditValue("2026-10-07T16:57:26.58+00:00", undefined, userNames)).toBe(
      formatDateTime("2026-10-07T16:57:26.58+00:00"),
    );
    expect(formatAuditValue("08:30:00", undefined, userNames)).toBe("۰۸:۳۰");
    expect(formatAuditValue(true, undefined, userNames)).toBe("بله");
    expect(formatAuditValue(false, undefined, userNames)).toBe("خیر");
    expect(formatAuditValue(1405, undefined, userNames)).toBe("۱۴۰۵");
    expect(formatAuditValue("+989121234567", undefined, userNames)).toBe("+۹۸۹۱۲۱۲۳۴۵۶۷");
    expect(formatAuditValue(null, "money", userNames)).toBe(emptyValue);
  });

  it("shownChanges_HidesIdsAndNormalizedCopiesAndLabelsTheRest", () => {
    const changes = shownChanges(
      {
        entityType: "Member",
        changes: [
          { field: "NormalizedFullName", oldValue: null, newValue: "سارا محمدی" },
          { field: "Id", oldValue: null, newValue: "01990000-0000-7000-8000-0000000000a1" },
          { field: "PhoneNumber", oldValue: null, newValue: "+989121234567" },
          { field: "FullName", oldValue: null, newValue: "سارا محمدی" },
        ],
      },
      userNames,
    );

    // In the labels file's order, not the database's: the name before the mobile.
    expect(changes.map((change) => change.label)).toEqual(["نام", "موبایل"]);
    expect(changes[0]!.after).toBe("سارا محمدی");
    expect(changes[0]!.before).toBe(emptyValue);
  });

  it("shownChanges_KindDependsOnTheRecord", () => {
    const payment = shownChanges(
      { entityType: "Payment", changes: [{ field: "Kind", oldValue: null, newValue: "Payment" }] },
      userNames,
    );
    const sms = shownChanges(
      {
        entityType: "Notification",
        changes: [{ field: "Kind", oldValue: null, newValue: "Birthday" }],
      },
      userNames,
    );

    expect(payment[0]!.after).toBe("پرداخت");
    expect(sms[0]!.after).toBe("تولد");
  });

  it("shownChanges_FieldWithNoLabel_KeepsItsNameAndComesLast", () => {
    const changes = shownChanges(
      {
        entityType: "Subscription",
        changes: [
          { field: "BrandNewField", oldValue: 1, newValue: 2 },
          { field: "UsedSessions", oldValue: 4, newValue: 5 },
        ],
      },
      userNames,
    );

    expect(changes.map((change) => change.label)).toEqual(["جلسات استفاده‌شده", "BrandNewField"]);
    expect(changes[0]).toMatchObject({ before: "۴", after: "۵" });
  });
});
