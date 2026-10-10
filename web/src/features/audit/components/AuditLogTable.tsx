import { ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { emptyValue, formatDateTime } from "@/lib/format";

import type { AuditLog } from "../api";
import { auditActionLabels, entityLabel, shownChanges } from "../labels";

const columns = 5;

const actionBadges = {
  Insert: "success",
  Update: "secondary",
  Delete: "destructive",
} as const;

interface AuditLogTableProps {
  rows: AuditLog[];
  /** A user's id to their full name, for the user ids a change records. */
  userNames: ReadonlyMap<string, string>;
  onMember: (memberId: string) => void;
  onRecord: (entityType: string, entityId: string) => void;
}

/** The audit log's rows, the latest first, as the API sends them (BUSINESS_RULES.md §11). */
export function AuditLogTable({ rows, userNames, onMember, onRecord }: AuditLogTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">زمان</th>
            <th className="py-2 text-start font-medium">کاربر</th>
            <th className="py-2 text-start font-medium">تغییر</th>
            <th className="py-2 text-start font-medium">مربوط به</th>
            <th className="py-2 text-start font-medium">
              <span className="sr-only">جزئیات</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <AuditLogRow
              key={row.id}
              row={row}
              userNames={userNames}
              onMember={onMember}
              onRecord={onRecord}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

interface AuditLogRowProps {
  row: AuditLog;
  userNames: ReadonlyMap<string, string>;
  onMember: (memberId: string) => void;
  onRecord: (entityType: string, entityId: string) => void;
}

/**
 * One change. Its fields open under the row: an update shows each field before and after, an
 * insert what was written, a delete what was removed.
 */
function AuditLogRow({ row, userNames, onMember, onRecord }: AuditLogRowProps) {
  const [open, setOpen] = useState(false);
  const what = `${auditActionLabels[row.action]} ${entityLabel(row.entityType)}`;
  const who = row.userFullName ?? (row.userId === null ? "سیستم" : "کاربر ناشناس");
  const changes = open ? shownChanges(row, userNames) : [];
  const memberId = row.memberId;

  return (
    <>
      <tr className="border-b align-top">
        <td className="py-2 whitespace-nowrap">{formatDateTime(row.occurredAt)}</td>
        <td className="py-2">
          <p>{who}</p>
          {row.ipAddress !== null && (
            <p className="text-xs text-muted-foreground" dir="ltr">
              {row.ipAddress}
            </p>
          )}
        </td>
        <td className="py-2">
          <span className="flex flex-wrap items-center gap-2">
            <Badge variant={actionBadges[row.action]}>{auditActionLabels[row.action]}</Badge>
            <span>{entityLabel(row.entityType)}</span>
          </span>
        </td>
        <td className="py-2">
          {memberId !== null && row.memberFullName !== null ? (
            <button
              type="button"
              className="font-medium hover:underline"
              aria-label={`تغییرهای ${row.memberFullName}`}
              onClick={() => onMember(memberId)}
            >
              {row.memberFullName}
            </button>
          ) : (
            <span className="text-muted-foreground">{emptyValue}</span>
          )}
        </td>
        <td className="py-2 text-end">
          <Button
            size="sm"
            variant="ghost"
            aria-expanded={open}
            aria-label={`جزئیات ${what}`}
            onClick={() => setOpen((current) => !current)}
          >
            {open ? <ChevronUp aria-hidden /> : <ChevronDown aria-hidden />}
            جزئیات
          </Button>
        </td>
      </tr>
      {open && (
        <tr className="border-b bg-muted/30">
          <td colSpan={columns} className="space-y-3 py-3">
            {changes.length === 0 ? (
              <p className="text-sm text-muted-foreground">جزئیات دیگری ثبت نشده است.</p>
            ) : (
              <table className="w-full text-sm" aria-label={`فیلدهای ${what}`}>
                <thead>
                  <tr className="text-muted-foreground">
                    <th className="pb-1 text-start font-medium">فیلد</th>
                    {row.action === "Update" ? (
                      <>
                        <th className="pb-1 text-start font-medium">قبل</th>
                        <th className="pb-1 text-start font-medium">بعد</th>
                      </>
                    ) : (
                      <th className="pb-1 text-start font-medium">
                        {row.action === "Insert" ? "مقدار" : "مقدار پیش از حذف"}
                      </th>
                    )}
                  </tr>
                </thead>
                <tbody>
                  {changes.map((change) => (
                    <tr key={change.field}>
                      <td className="py-1 pe-3 text-muted-foreground">{change.label}</td>
                      {row.action === "Update" ? (
                        <>
                          <td className="py-1 pe-3">{change.before}</td>
                          <td className="py-1 font-medium">{change.after}</td>
                        </>
                      ) : (
                        <td className="py-1">
                          {row.action === "Insert" ? change.after : change.before}
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <Button
              size="sm"
              variant="outline"
              onClick={() => onRecord(row.entityType, row.entityId)}
            >
              همهٔ تغییرهای این {entityLabel(row.entityType)}
            </Button>
          </td>
        </tr>
      )}
    </>
  );
}
