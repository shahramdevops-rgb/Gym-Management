import { Link } from "react-router";

import { paths } from "@/app/paths";
import { SessionsBar } from "@/components/SessionsBar";
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
import { lowSessionsThreshold } from "@/features/attendance/renewal";
import { stateEdge } from "@/lib/edges";
import { emptyValue, formatMoney, formatPhone, toPersianDigits } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";
import { cn } from "@/lib/utils";

import type { Member } from "../api";

interface MembersTableProps {
  members: Member[];
  /**
   * Check-out from the row of someone inside, shown only where a caller passes it (the member
   * list). It opens a confirmation first (BUSINESS_RULES.md §7 *Confirming at the front desk*).
   * There is no check-in here: that happens on the locker map, where the locker is chosen
   * (roadmap 6.5.5).
   */
  deskActions?: {
    onCheckOut: (member: Member) => void;
  };
}

/**
 * Search results and the member list: name (a link to the profile) with its tags, phone, the
 * sessions of their plan, debt. No status column (removed by the developer, roadmap 6.5.34): the
 * filter above the list already splits active from inactive.
 */
export function MembersTable({ members, deskActions }: MembersTableProps) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>نام</TableHead>
          <TableHead>موبایل</TableHead>
          <TableHead>جلسات</TableHead>
          <TableHead>بدهی</TableHead>
          {deskActions !== undefined && (
            <TableHead>
              <span className="sr-only">عملیات</span>
            </TableHead>
          )}
        </TableRow>
      </TableHeader>
      <TableBody>
        {members.map((member) => (
          <TableRow key={member.id}>
            {/* Edged at its start in the page's tone, like a locker door (`tone-row-edge`). */}
            <TableCell className="tone-row-edge">
              <div className="flex flex-wrap items-center gap-1.5">
                <Link
                  to={paths.member(member.id)}
                  className="font-medium underline-offset-4 hover:underline"
                >
                  {member.fullName}
                </Link>
                {/* BUSINESS_RULES.md §2: their latest visit was a single visit. */}
                {member.lastVisitWasSingleSession && <Badge variant="destructive">تک‌جلسه</Badge>}
                {/* BUSINESS_RULES.md §2: the plan is over and nothing was bought after it. */}
                {member.planEnded && <Badge variant="warning">پلن تمام‌شده</Badge>}
              </div>
            </TableCell>
            <TableCell label="موبایل">
              <PhoneNumber value={member.phoneNumber} />
            </TableCell>
            <TableCell label="جلسات">
              <PlanSessions plan={member.plan ?? null} />
            </TableCell>
            <TableCell label="بدهی">
              <MemberDebt value={member.debt} />
            </TableCell>
            {deskActions !== undefined && (
              <TableCell>
                <DeskButtons member={member} {...deskActions} />
              </TableCell>
            )}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * For someone inside, where they are and the check-out button; nothing for anyone else. Inside is
 * decided by the open visit the API reports, the same test check-in refuses on (BUSINESS_RULES.md
 * §7).
 */
function DeskButtons({
  member,
  onCheckOut,
}: {
  member: Member;
  onCheckOut: (member: Member) => void;
}) {
  const visit = member.currentVisit ?? null;
  if (visit === null) {
    return null;
  }

  return (
    <div className="flex items-center justify-end gap-2">
      {/* Green like «داخل باشگاه» in the history, and «ثبت خروج» as on the board (not a bare «خروج», which is also signing out). */}
      <Badge variant="success">
        داخل باشگاه —{" "}
        {visit.lockerNumber === null ? "بدون کمد" : `کمد ${toPersianDigits(visit.lockerNumber)}`}
      </Badge>
      <Button
        size="sm"
        variant="ghost"
        className={cn(stateEdge.success, "hover:bg-success/10 hover:text-success-ink")}
        onClick={() => onCheckOut(member)}
      >
        ثبت خروج
      </Button>
    </div>
  );
}

/**
 * The bar the "currently inside" board shows, for the plan the API picked: the current one, else
 * the queued one, else the one that ended last (BUSINESS_RULES.md §2). Nothing for a member with no
 * plan; single visits never have one.
 */
function PlanSessions({ plan }: { plan: Member["plan"] }) {
  if (plan === null || plan === undefined) {
    return <span className="text-muted-foreground">{emptyValue}</span>;
  }

  return (
    <SessionsBar
      total={plan.totalSessions}
      used={plan.usedSessions}
      remaining={plan.remainingSessions}
      lowThreshold={lowSessionsThreshold}
      className="max-w-32"
    />
  );
}

/**
 * The amount rather than a "بدهکار" badge (task 4.7): the gym runs open accounts, so whether a
 * member owes anything matters less than how much, and the front desk reads it off the list
 * before opening the profile (BUSINESS_RULES.md §5 Member debt).
 */
function MemberDebt({ value }: { value: Member["debt"] }) {
  // Solid red like «پرداخت‌نشده».
  return isPositiveMoney(value) ? <Badge variant="destructive">{formatMoney(value)}</Badge> : null;
}

/** Solid like every state tag: green while they may come in, grey once deactivated. */
export function MemberStatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <Badge variant={isActive ? "success" : "secondary"}>{isActive ? "فعال" : "غیرفعال"}</Badge>
  );
}

/**
 * A phone number laid out left to right. Without dir="ltr" the right-to-left page would put
 * the three digit groups in reverse order. `inline-block` keeps it aligned with the column's
 * start, like the text around it.
 */
export function PhoneNumber({ value }: { value: string }) {
  return (
    <span dir="ltr" className="inline-block">
      {formatPhone(value)}
    </span>
  );
}
