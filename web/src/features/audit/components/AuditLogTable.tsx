import { ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
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
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>زمان</TableHead>
          <TableHead>کاربر</TableHead>
          <TableHead>تغییر</TableHead>
          <TableHead>مربوط به</TableHead>
          <TableHead>
            <span className="sr-only">جزئیات</span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((row) => (
          <AuditLogRow
            key={row.id}
            row={row}
            userNames={userNames}
            onMember={onMember}
            onRecord={onRecord}
          />
        ))}
      </TableBody>
    </Table>
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
      <TableRow joins={open ? "below" : undefined} className="align-top">
        <TableCell className="whitespace-nowrap">{formatDateTime(row.occurredAt)}</TableCell>
        <TableCell label="کاربر">
          <p>{who}</p>
          {row.ipAddress !== null && (
            <p className="text-xs text-muted-foreground" dir="ltr">
              {row.ipAddress}
            </p>
          )}
        </TableCell>
        <TableCell label="تغییر">
          <span className="flex flex-wrap items-center gap-2">
            <Badge variant={actionBadges[row.action]}>{auditActionLabels[row.action]}</Badge>
            <span>{entityLabel(row.entityType)}</span>
          </span>
        </TableCell>
        <TableCell label="مربوط به">
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
        </TableCell>
        <TableCell className="text-end">
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
        </TableCell>
      </TableRow>
      {open && (
        <TableRow joins="above" className="bg-muted/30">
          <TableCell colSpan={columns} className="space-y-3 py-3">
            {changes.length === 0 ? (
              <p className="text-sm text-muted-foreground">جزئیات دیگری ثبت نشده است.</p>
            ) : (
              <Table aria-label={`فیلدهای ${what}`}>
                <TableHeader>
                  <TableRow className="text-muted-foreground">
                    <TableHead className="pb-1 text-start font-medium">فیلد</TableHead>
                    {row.action === "Update" ? (
                      <>
                        <TableHead className="pb-1 text-start font-medium">قبل</TableHead>
                        <TableHead className="pb-1 text-start font-medium">بعد</TableHead>
                      </>
                    ) : (
                      <TableHead className="pb-1 text-start font-medium">
                        {row.action === "Insert" ? "مقدار" : "مقدار پیش از حذف"}
                      </TableHead>
                    )}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {changes.map((change) => (
                    <TableRow key={change.field}>
                      <TableCell className="py-1 pe-3 text-muted-foreground">
                        {change.label}
                      </TableCell>
                      {row.action === "Update" ? (
                        <>
                          <TableCell label="قبل" className="py-1 pe-3">
                            {change.before}
                          </TableCell>
                          <TableCell label="بعد" className="py-1 font-medium">
                            {change.after}
                          </TableCell>
                        </>
                      ) : (
                        <TableCell
                          label={row.action === "Insert" ? "مقدار" : "مقدار پیش از حذف"}
                          className="py-1"
                        >
                          {row.action === "Insert" ? change.after : change.before}
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
            <Button
              size="sm"
              variant="outline"
              onClick={() => onRecord(row.entityType, row.entityId)}
            >
              همهٔ تغییرهای این {entityLabel(row.entityType)}
            </Button>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}
