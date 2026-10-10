import { payableKindLabels } from "@/features/payables/api";
import { paymentMethodLabels } from "@/features/payments/api";
import { serviceChargeKindLabels } from "@/features/serviceCharges/api";
import { smsDeliveryLabels, smsKindLabels, smsStatusLabels } from "@/features/sms/labels";
import { emptyValue, formatDate, formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";

import type { AuditAction, AuditChange, AuditLog } from "./api";
import auditFields from "./auditFields.json";

/**
 * How a field's value is shown. Without one, the value's own shape decides: a date, a moment, a
 * time of day, yes/no, a number or text.
 */
type FieldFormat =
  | "hidden"
  | "money"
  | "rial"
  | "reference"
  | "user"
  | "paymentKind"
  | "paymentMethod"
  | "payableKind"
  | "serviceChargeKind"
  | "notificationKind"
  | "notificationStatus"
  | "smsDelivery"
  | "revocationReason";

interface FieldSpec {
  label?: string;
  format?: FieldFormat;
}

/**
 * The Persian names, from `auditFields.json`. A JSON file rather than TypeScript so the backend can
 * read it too: a test there fails when an audited field has no label here.
 */
const entities: Record<string, string> = auditFields.entities;
const fields = auditFields.fields as Record<string, FieldSpec>;
const entityFields = auditFields.entityFields as Record<string, Record<string, FieldSpec>>;

/** The order the fields are listed in: the JSON's, so related fields sit together. */
const fieldOrder = new Map(Object.keys(fields).map((field, index) => [field, index]));

export const auditActionLabels: Record<AuditAction, string> = {
  Insert: "ثبت",
  Update: "ویرایش",
  Delete: "حذف",
};

/** The rows the screen hides unless asked (BUSINESS_RULES.md §11 *The audit screen*). */
export const signInEntityTypes: readonly string[] = ["RefreshToken", "TrustedDevice"];

/** Every kind of record the log may hold, with its Persian name, in the JSON's order. */
export const auditEntityTypes: readonly { type: string; label: string }[] = Object.entries(
  entities,
).map(([type, label]) => ({ type, label }));

const enumLabels: Record<string, Record<string, string>> = {
  paymentKind: { Payment: "پرداخت", Refund: "بازپرداخت" },
  paymentMethod: paymentMethodLabels,
  payableKind: payableKindLabels,
  serviceChargeKind: serviceChargeKindLabels,
  notificationKind: smsKindLabels,
  notificationStatus: smsStatusLabels,
  smsDelivery: smsDeliveryLabels,
  revocationReason: {
    Rotated: "تمدید شد",
    ReuseDetected: "استفادهٔ دوباره، مشکوک",
    Logout: "خروج",
    UserInactive: "کاربر غیرفعال شد",
    PasswordChanged: "تغییر رمز",
    PasswordReset: "بازنشانی رمز",
    PasswordSetOnServer: "رمز روی سرور تعیین شد",
  },
};

/** A kind of record by its Persian name; one with no name yet keeps its English one. */
export function entityLabel(entityType: string): string {
  return entities[entityType] ?? entityType;
}

function specOf(entityType: string, field: string): FieldSpec {
  return entityFields[entityType]?.[field] ?? fields[field] ?? {};
}

const isoDate = /^\d{4}-\d{2}-\d{2}$/;
const isoMoment = /^\d{4}-\d{2}-\d{2}T/;
const timeOfDay = /^\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/;

/** Another record's id says nothing to the Owner; its first characters tell two apart. */
function shortCode(value: string): string {
  return value.slice(0, 8);
}

/**
 * One stored value as the Owner reads it: money as Toman, dates in Jalali, the kinds in Persian, a
 * user's id as their name. `userNames` maps a user's id to their full name.
 */
export function formatAuditValue(
  value: unknown,
  format: FieldFormat | undefined,
  userNames: ReadonlyMap<string, string>,
): string {
  if (value === null || value === undefined) {
    return emptyValue;
  }

  switch (format) {
    case "money":
      return formatMoney(value as number | string);
    case "rial":
      return formatMoney(Number(value) / 10);
    case "user":
      return userNames.get(String(value)) ?? shortCode(String(value));
    case "reference":
      return shortCode(String(value));
    case undefined:
    case "hidden":
      break;
    default:
      return enumLabels[format]?.[String(value)] ?? String(value);
  }

  if (typeof value === "boolean") {
    return value ? "بله" : "خیر";
  }

  if (typeof value === "number") {
    return toPersianDigits(value);
  }

  if (typeof value === "string") {
    if (isoDate.test(value)) {
      return formatDate(value);
    }
    if (isoMoment.test(value)) {
      return formatDateTime(value);
    }
    if (timeOfDay.test(value)) {
      return toPersianDigits(value.slice(0, 5));
    }
    return toPersianDigits(value);
  }

  return JSON.stringify(value);
}

/** One change as the screen lists it: the field's Persian label and both values formatted. */
export interface ShownChange {
  field: string;
  label: string;
  before: string;
  after: string;
}

/**
 * The fields worth showing, formatted and in a fixed order. Ids, `Normalized…` copies and internal
 * links are left out (BUSINESS_RULES.md §11 *The audit screen*).
 */
export function shownChanges(
  row: Pick<AuditLog, "entityType" | "changes">,
  userNames: ReadonlyMap<string, string>,
): ShownChange[] {
  return row.changes
    .map((change: AuditChange) => ({ change, spec: specOf(row.entityType, change.field) }))
    .filter(({ spec }) => spec.format !== "hidden")
    .sort(
      (a, b) =>
        (fieldOrder.get(a.change.field) ?? Number.MAX_SAFE_INTEGER) -
          (fieldOrder.get(b.change.field) ?? Number.MAX_SAFE_INTEGER) ||
        a.change.field.localeCompare(b.change.field),
    )
    .map(({ change, spec }) => ({
      field: change.field,
      label: spec.label ?? change.field,
      before: formatAuditValue(change.oldValue, spec.format, userNames),
      after: formatAuditValue(change.newValue, spec.format, userNames),
    }));
}
